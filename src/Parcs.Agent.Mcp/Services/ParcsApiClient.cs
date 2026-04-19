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
///   POST /api/Modules                      — upload module binaries, returns { moduleId }
///   POST /api/Jobs                         — create job with input files, returns { jobId }
///   POST /api/AsynchronousJobRuns          — submit job for async execution (202 Accepted)
///   GET  /api/Jobs/{jobId}/stream          — SSE stream of job status events
///   GET  /api/JobOutputs/{jobId}           — download output zip
///
/// Why async submit + SSE instead of /api/SynchronousJobRuns:
///   The synchronous endpoint blocks for the entire job duration (minutes). During that
///   time no bytes flow back through the MCP SSE connection, so proxies and load-balancers
///   treat the connection as idle and close it. The async + SSE approach emits heartbeat
///   comments every few seconds, keeping every hop in the chain alive.
/// </summary>
public sealed class ParcsApiClient
{
    private readonly string _baseUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ParcsApiClient> _logger;

    private readonly string _callbackUrl;

    public ParcsApiClient(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ParcsApiClient> logger)
    {
        _baseUrl           = configuration["Parcs:HostUrl"]
            ?? throw new InvalidOperationException("Parcs:HostUrl is not configured.");
        _callbackUrl       = configuration["Parcs:CallbackUrl"] ?? "http://parcs-agent-mcp:8080/noop";
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
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
            content.Add(fileContent, "BinaryFiles", filename);
        }

        var response = await _baseUrl
            .AppendPathSegment("api/Modules")
            .PostAsync(content, cancellationToken: ct);

        var json = await response.GetStringAsync();
        using var doc = JsonDocument.Parse(json);
        var moduleId = doc.RootElement.GetProperty("moduleId").GetInt64();

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

        IFlurlResponse jobResponse;
        try
        {
            jobResponse = await _baseUrl
                .AppendPathSegment("api/Jobs")
                .PostAsync(content, cancellationToken: ct);
        }
        catch (Flurl.Http.FlurlHttpException ex)
        {
            var body = await ex.GetResponseStringAsync();
            _logger.LogError("POST /api/Jobs failed {Status}: {Body}", ex.StatusCode, body);
            throw new InvalidOperationException($"POST /api/Jobs failed {ex.StatusCode}: {body}", ex);
        }

        var json = await jobResponse.GetStringAsync();
        using var doc = JsonDocument.Parse(json);
        var jobId = doc.RootElement.GetProperty("jobId").GetInt64();

        _logger.LogInformation("Job created — id={JobId}", jobId);
        return jobId;
    }

    /// <summary>
    /// Submits a job for asynchronous execution then blocks until the job reaches a
    /// terminal status by consuming a Server-Sent Events stream from the Host API.
    ///
    /// Flow:
    ///   1. POST /api/AsynchronousJobRuns  — returns 202 immediately
    ///   2. GET  /api/Jobs/{jobId}/stream  — SSE events + heartbeat comments keep
    ///                                       every proxy in the chain alive
    ///   3. Returns when status == Completed; throws on Failed / Cancelled.
    /// </summary>
    public async Task RunJobAndWaitAsync(
        long jobId,
        IReadOnlyDictionary<string, string>? arguments = null,
        CancellationToken ct = default)
    {
        // Step 1 — submit asynchronously (returns 202 immediately)
        _logger.LogInformation("Submitting job async: jobId={JobId}", jobId);

        await _baseUrl
            .AppendPathSegment("api/AsynchronousJobRuns")
            .PostJsonAsync(new
            {
                jobId,
                arguments   = (object)(arguments ?? new Dictionary<string, string>()),
                callbackUrl = _callbackUrl,
            }, cancellationToken: ct);

        // Step 2 — stream status events until terminal
        _logger.LogInformation("Streaming job status: jobId={JobId}", jobId);

        var streamUrl = _baseUrl.TrimEnd('/') + $"/api/Jobs/{jobId}/stream";

        // Use a plain HttpClient — Flurl doesn't support streaming SSE responses.
        // InfiniteTimeSpan because the stream itself drives liveness via heartbeats.
        using var http = _httpClientFactory.CreateClient();
        http.Timeout = Timeout.InfiniteTimeSpan;

        using var response = await http.GetAsync(
            streamUrl, HttpCompletionOption.ResponseHeadersRead, ct);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader       = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break; // stream closed by server

            // Skip SSE comment lines (heartbeats) and blank separator lines
            if (line.StartsWith(':') || string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data:"))
                continue;

            var json = line["data:".Length..].Trim();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errProp))
                throw new InvalidOperationException($"Job stream error: {errProp.GetString()}");

            if (!root.TryGetProperty("status", out var statusProp))
                continue;

            var status = statusProp.GetString();
            _logger.LogInformation("Job {JobId} status: {Status}", jobId, status);

            if (status is "Completed")
                return;

            if (status is "Failed" or "Cancelled")
            {
                var failures = root.TryGetProperty("failures", out var fp)
                    ? string.Join("; ", fp.EnumerateArray().Select(f => f.GetString()))
                    : "unknown error";
                throw new InvalidOperationException($"Job {jobId} {status}: {failures}");
            }
        }

        throw new InvalidOperationException($"Job {jobId} SSE stream ended without a terminal status.");
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

