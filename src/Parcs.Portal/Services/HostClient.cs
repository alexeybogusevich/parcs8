using Flurl.Http;
using Flurl.Http.Content;
using LanguageExt.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Parcs.Portal.Configuration;
using Parcs.Portal.Models.Host;
using Parcs.Portal.Models.Host.Requests;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Services
{
    public class HostClient : IHostClient
    {
        private readonly FlurlClient _flurlClient;
        private readonly HostConfiguration _hostConfiguration;

        public HostClient(HttpClient httpClient, IOptions<HostConfiguration> hostOptions)
        {
            _flurlClient = new FlurlClient(httpClient);
            _hostConfiguration = hostOptions.Value;
        }

        public Task<GetModuleHostResponse> GetModuleAsync(long id, CancellationToken cancellationToken = default)
        {
            return _flurlClient
                .Request(string.Format(_hostConfiguration.GetModuleEndpoint, id))
                .GetJsonAsync<GetModuleHostResponse>(cancellationToken);
        }

        public Task<IEnumerable<GetPlainModuleHostResponse>> GetModulesAsync(CancellationToken cancellationToken = default)
        {
            return _flurlClient
                .Request(_hostConfiguration.GetModulesEndpoint)
                .GetJsonAsync<IEnumerable<GetPlainModuleHostResponse>>(cancellationToken);
        }

        public async Task<Result<CreateModuleHostResponse>> PostModuleAsync(CreateModuleHostRequest createModuleHostRequest, CancellationToken cancellationToken = default)
        {
            var binaryFiles = createModuleHostRequest.BinaryFiles;
            var binaryFileStreams = binaryFiles.Select(file => file.OpenReadStream()).ToList();

            void MultipartContent(CapturedMultipartContent multipartContentBuilder)
            {
                multipartContentBuilder.AddString(nameof(CreateModuleHostRequest.Name), createModuleHostRequest.Name);

                for (int i = 0; i < binaryFileStreams.Count; ++i)
                {
                    multipartContentBuilder.AddFile($"{nameof(CreateModuleHostRequest.BinaryFiles)}{i}", binaryFileStreams[i], binaryFiles[i].FileName);
                }
            }

            try
            {
                var response = await _flurlClient
                    .Request(_hostConfiguration.PostModulesEndpoint)
                    .PostMultipartAsync(MultipartContent, cancellationToken);

                if (response.StatusCode == StatusCodes.Status400BadRequest)
                {
                    var problemDetails = await response.GetJsonAsync<ProblemDetails>();
                    return new Result<CreateModuleHostResponse>(new HostException(problemDetails));
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
                .DeleteAsync(cancellationToken);
        }

        public Task<GetJobHostResponse> GetJobAsync(long jobId, CancellationToken cancellationToken = default)
        {
            return _flurlClient
                .Request(string.Format(_hostConfiguration.GetJobEndpoint, jobId))
                .GetJsonAsync<GetJobHostResponse>(cancellationToken);
        }

        public Task<IEnumerable<GetPlainJobHostResponse>> GetJobsAsync(CancellationToken cancellationToken = default)
        {
            return _flurlClient
                .Request(_hostConfiguration.GetJobsEndpoint)
                .GetJsonAsync<IEnumerable<GetPlainJobHostResponse>>(cancellationToken);
        }

        public async Task<Result<CreateJobHostResponse>> PostJobAsync(CreateJobHostRequest createJobHostRequest, CancellationToken cancellationToken = default)
        {
            var binaryFiles = createJobHostRequest.InputFiles;
            var binaryFileStreams = binaryFiles.Select(file => file.OpenReadStream()).ToList();

            void MultipartContent(CapturedMultipartContent multipartContentBuilder)
            {
                multipartContentBuilder.AddString(nameof(CreateJobHostRequest.ModuleId), createJobHostRequest.ModuleId.ToString());
                multipartContentBuilder.AddString(nameof(CreateJobHostRequest.AssemblyName), createJobHostRequest.AssemblyName);
                multipartContentBuilder.AddString(nameof(CreateJobHostRequest.ClassName), createJobHostRequest.ClassName);

                for (int i = 0; i < binaryFileStreams.Count; ++i)
                {
                    multipartContentBuilder.AddFile($"{nameof(CreateJobHostRequest.InputFiles)}{i}", binaryFileStreams[i], binaryFiles[i].FileName);
                }
            }

            try
            {
                var response = await _flurlClient
                    .Request(_hostConfiguration.PostJobsEndpoint)
                    .PostMultipartAsync(MultipartContent, cancellationToken);

                if (response.StatusCode == StatusCodes.Status400BadRequest)
                {
                    var problemDetails = await response.GetJsonAsync<ProblemDetails>();
                    return new Result<CreateJobHostResponse>(new HostException(problemDetails));
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

        public async Task<Result<bool>> PostRunAsync(RunJobHostRequest runJobHostRequest, CancellationToken cancellationToken = default)
        {
            var response = await _flurlClient
                .Request(_hostConfiguration.PostAsynchronousRunsEndpoint)
                .PostJsonAsync(runJobHostRequest, cancellationToken);

            if (response.StatusCode == StatusCodes.Status400BadRequest)
            {
                var problemDetails = await response.GetJsonAsync<ProblemDetails>();
                return new Result<bool>(new HostException(problemDetails));
            }

            return true;
        }
    }
}