using System.IO.Compression;
using System.Text.Json;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Parcs.Agent.Mcp.Services;

/// <summary>
/// HTTP client for the PARCS Host API.
///
/// Endpoints used:
///   POST /api/Modules                     — upload module binaries, returns { id }
///   POST /api/Jobs                        — create job with input files, returns { id }
///   POST /api/SynchronousJobRuns          — run a job synchronously (blocking)
///   GET  /api/JobOutputs/{jobId}          — download output zip
/// </summary>
public sealed class ParcsApiClient
{
    private readonly string _baseUrl;
    private readonly ILogger<ParcsApiClient> _logger;

    public ParcsApiClient(IConfiguration configuration, ILogger<ParcsApiClient> logger)
    {
        _baseUrl = configuration["Parcs:HostUrl"]
            ?? throw new InvalidOperationException("Parcs:HostUrl is not configured.");
        _logger = logger;
    }

    /// <summary>
    /// Uploads a set of DLL files as a PARCS module.
    /// Returns the numeric module ID assigned by the host.
    /// </summary>
    public async Task<long> UploadModuleAsync(
        IEnumerable<(string Filename, byte[] Bytes)> files,
        string moduleName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Uploading PARCS module '{Name}'", moduleName);

        // Build multipart form
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(moduleName), "Name");
        foreach (var (filename, bytes) in files)
        {
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "Binaries", filename);
        }

        var response = await _baseUrl
            .AppendPathSegment("api/Modules")
            .PostAsync(content, cancellationToken: ct);

        var json = await response.GetStringAsync();
        using var doc = JsonDocument.Parse(json);
        var moduleId = doc.RootElement.GetProperty("id").GetInt64();

        _logger.LogInformation("Module uploaded — id={ModuleId}", moduleId);
        return moduleId;
    }

    /// <summary>
    /// Creates a PARCS job with the provided input files and module metadata.
    /// Returns the numeric job ID.
    /// </summary>
    public async Task<long> CreateJobAsync(
        long moduleId,
        string assemblyName,
        string className,
        IEnumerable<(string Filename, byte[] Bytes)> inputFiles,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Creating job: moduleId={ModuleId} class={Class}", moduleId, className);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(moduleId.ToString()), "ModuleId");
        content.Add(new StringContent(assemblyName), "AssemblyName");
        content.Add(new StringContent(className), "ClassName");

        foreach (var (key, value) in arguments)
        {
            content.Add(new StringContent(value), $"Arguments[{key}]");
        }

        foreach (var (filename, bytes) in inputFiles)
        {
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "InputFiles", filename);
        }

        var jobResponse = await _baseUrl
            .AppendPathSegment("api/Jobs")
            .PostAsync(content, cancellationToken: ct);

        var json = await jobResponse.GetStringAsync();
        using var doc = JsonDocument.Parse(json);
        var jobId = doc.RootElement.GetProperty("id").GetInt64();

        _logger.LogInformation("Job created — id={JobId}", jobId);
        return jobId;
    }

    /// <summary>
    /// Runs a job synchronously (blocks until completion or cancellation).
    /// </summary>
    public async Task RunJobAsync(
        long jobId,
        IReadOnlyDictionary<string, string>? arguments = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Running job synchronously: jobId={JobId}", jobId);

        await _baseUrl
            .AppendPathSegment("api/SynchronousJobRuns")
            .PostJsonAsync(new { jobId, arguments = (object)(arguments ?? new Dictionary<string, string>()) },
                           cancellationToken: ct);

        _logger.LogInformation("Job {JobId} completed", jobId);
    }

    /// <summary>
    /// Downloads the output zip for <paramref name="jobId"/> and extracts <paramref name="filename"/>.
    /// Returns the file contents as a byte array, or null if the file is not present.
    /// </summary>
    public async Task<byte[]?> GetJobOutputFileAsync(long jobId, string filename, CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching output for job {JobId}, file '{File}'", jobId, filename);

        var outputResponse = await _baseUrl
            .AppendPathSegment($"api/JobOutputs/{jobId}")
            .GetAsync(cancellationToken: ct);

        var zipBytes = await outputResponse.GetBytesAsync();

        using var zipStream = new MemoryStream(zipBytes);
        using var archive   = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entry = archive.GetEntry(filename)
                 ?? archive.Entries.FirstOrDefault(e =>
                        string.Equals(e.Name, filename, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            _logger.LogWarning("File '{File}' not found in output zip for job {JobId}", filename, jobId);
            return null;
        }

        using var entryStream = entry.Open();
        using var ms          = new MemoryStream();
        await entryStream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}

