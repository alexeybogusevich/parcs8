using Flurl.Http;
using Flurl.Http.Configuration;
using Flurl.Http.Content;
using Microsoft.Extensions.Options;
using Parcs.Portal.Configuration;
using Parcs.Portal.Models;
using Parcs.Portal.Models.Host;
using Parcs.Portal.Models.Host.Requests;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;
using System.Text.Json.Serialization;

namespace Parcs.Portal.Services
{
    public class HostClient : IHostClient
    {
        private readonly FlurlClient _flurlClient;
        private readonly HostConfiguration _hostConfiguration;
        private readonly IDocumentResponseProcessor _documentResponseProcessor;

        public HostClient(HttpClient httpClient, IOptions<HostConfiguration> hostOptions, IDocumentResponseProcessor documentResponseProcessor)
        {
            _flurlClient = new FlurlClient(httpClient);
            _flurlClient.Settings.JsonSerializer = new DefaultJsonSerializer(new()
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(), },
            });
            _hostConfiguration = hostOptions.Value;
            _documentResponseProcessor = documentResponseProcessor;
        }

        public async Task<GetModuleHostResponse> GetModuleAsync(long id, CancellationToken cancellationToken = default)
        {
            var response = _flurlClient
                .Request(string.Format(_hostConfiguration.GetModuleEndpoint, id));

            var stringResponse = await response.GetStringAsync();

            return await response
                .GetJsonAsync<GetModuleHostResponse>(cancellationToken: cancellationToken);
        }

        public Task<IEnumerable<GetPlainModuleHostResponse>> GetModulesAsync(CancellationToken cancellationToken = default)
        {
            return _flurlClient
                .Request(_hostConfiguration.GetModulesEndpoint)
                .GetJsonAsync<IEnumerable<GetPlainModuleHostResponse>>(cancellationToken: cancellationToken);
        }

        public async Task<CreateModuleHostResponse> PostModuleAsync(CreateModuleHostRequest createModuleHostRequest, CancellationToken cancellationToken = default)
        {
            var binaryFiles = createModuleHostRequest.BinaryFiles.ToList();
            var binaryFileStreams = binaryFiles.Select(file => file.OpenReadStream()).ToList();

            void MultipartContent(CapturedMultipartContent multipartContentBuilder)
            {
                multipartContentBuilder.AddString(nameof(CreateModuleHostRequest.Name), createModuleHostRequest.Name);

                for (int i = 0; i < binaryFileStreams.Count; ++i)
                {
                    multipartContentBuilder.AddFile(nameof(CreateModuleHostRequest.BinaryFiles), binaryFileStreams[i], binaryFiles[i].Name);
                }
            }

            try
            {
                using var response = await _flurlClient
                    .Request(_hostConfiguration.PostModulesEndpoint)
                    .AllowHttpStatus(StatusCodes.Status400BadRequest.ToString())
                    .PostMultipartAsync(MultipartContent, cancellationToken: cancellationToken);

                if (response.StatusCode == StatusCodes.Status400BadRequest)
                {
                    var problemDetails = await response.GetJsonAsync<ExtendedProblemDetails>();
                    throw new HostException(problemDetails);
                }

                return await response.GetJsonAsync<CreateModuleHostResponse>();
            }
            finally
            {
                foreach (var stream in binaryFileStreams)
                {
                    stream.Dispose();
                }
            }
        }

        public Task DeleteModuleAsync(long id, CancellationToken cancellationToken = default)
        {
            return _flurlClient
                .Request(string.Format(_hostConfiguration.DeleteModulesEndpoint, id))
                .DeleteAsync(cancellationToken: cancellationToken);
        }

        public Task<GetJobHostResponse> GetJobAsync(long jobId, CancellationToken cancellationToken = default)
        {
            return _flurlClient
                .Request(string.Format(_hostConfiguration.GetJobEndpoint, jobId))
                .GetJsonAsync<GetJobHostResponse>(cancellationToken: cancellationToken);
        }

        public async Task<DocumentDownloadResponse> GetJobOutputAsync(long jobId, CancellationToken cancellationToken = default)
        {
            using var response = await _flurlClient
                .Request(string.Format(_hostConfiguration.GetJobOutputEndpoint, jobId))
                .SendAsync(HttpMethod.Get, cancellationToken: cancellationToken);

            var responseHeaders = response.Headers.ToDictionary(h => h.Name, h => h.Value);
            var bytes = await response.GetBytesAsync();

            return _documentResponseProcessor.Parse(bytes, responseHeaders);
        }

        public Task<IEnumerable<GetJobHostResponse>> GetJobsAsync(CancellationToken cancellationToken = default)
        {
            return _flurlClient
                .Request(_hostConfiguration.GetJobsEndpoint)
                .GetJsonAsync<IEnumerable<GetJobHostResponse>>(cancellationToken: cancellationToken);
        }

        public async Task<CreateJobHostResponse> PostJobAsync(CreateJobHostRequest createJobHostRequest, CancellationToken cancellationToken = default)
        {
            var binaryFiles = createJobHostRequest.InputFiles.ToList();
            var binaryFileStreams = binaryFiles.Select(file => file.OpenReadStream()).ToList();

            void MultipartContent(CapturedMultipartContent multipartContentBuilder)
            {
                multipartContentBuilder.AddString(nameof(CreateJobHostRequest.ModuleId), createJobHostRequest.ModuleId.ToString());

                for (int i = 0; i < binaryFileStreams.Count; ++i)
                {
                    multipartContentBuilder.AddFile(nameof(CreateJobHostRequest.InputFiles), binaryFileStreams[i], binaryFiles[i].Name);
                }

                multipartContentBuilder.AddString(nameof(CreateJobHostRequest.AssemblyName), createJobHostRequest.AssemblyName);
                multipartContentBuilder.AddString(nameof(CreateJobHostRequest.ClassName), createJobHostRequest.ClassName);
            }

            try
            {
                using var response = await _flurlClient
                    .Request(_hostConfiguration.PostJobsEndpoint)
                    .AllowHttpStatus(StatusCodes.Status400BadRequest.ToString())
                    .PostMultipartAsync(MultipartContent, cancellationToken: cancellationToken);

                if (response.StatusCode == StatusCodes.Status400BadRequest)
                {
                    var problemDetails = await response.GetJsonAsync<ExtendedProblemDetails>();
                    throw new HostException(problemDetails);
                }

                return await response.GetJsonAsync<CreateJobHostResponse>();
            }
            finally
            {
                foreach (var stream in binaryFileStreams)
                {
                    stream.Dispose();
                }
            }
        }

        public async Task<CloneJobHostResponse> PostCloneJobAsync(long jobId, CancellationToken cancellationToken = default)
        {
            using var response = await _flurlClient
                .Request(string.Format(_hostConfiguration.PostCloneJobsEndpoint, jobId))
                .PostAsync(cancellationToken: cancellationToken);

            return await response.GetJsonAsync<CloneJobHostResponse>();
        }

        public Task PutJobAsync(long jobId, CancellationToken cancellationToken = default)
        {
            return _flurlClient
                .Request(string.Format(_hostConfiguration.PutJobEndpoint, jobId))
                .PutAsync(cancellationToken: cancellationToken);
        }

        public async Task<bool> PostJobRunAsync(RunJobHostRequest runJobHostRequest, CancellationToken cancellationToken = default)
        {
            var response = await _flurlClient
                .Request(_hostConfiguration.PostAsynchronousRunsEndpoint)
                .AllowHttpStatus(StatusCodes.Status400BadRequest.ToString())
                .PostJsonAsync(runJobHostRequest, cancellationToken: cancellationToken);

            if (response.StatusCode == StatusCodes.Status400BadRequest)
            {
                var problemDetails = await response.GetJsonAsync<ExtendedProblemDetails>();
                throw new HostException(problemDetails);
            }

            return true;
        }
    }
}