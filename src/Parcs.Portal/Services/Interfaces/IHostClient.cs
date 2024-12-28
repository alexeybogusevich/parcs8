using Parcs.Portal.Models;
using Parcs.Portal.Models.Host.Requests;
using Parcs.Portal.Models.Host.Responses;

namespace Parcs.Portal.Services.Interfaces
{
    public interface IHostClient
    {
        Task<bool> PostJobRunAsync(RunJobHostRequest runJobHostRequest, CancellationToken cancellationToken = default);

        Task<GetJobHostResponse> GetJobAsync(long jobId, CancellationToken cancellationToken = default);

        Task<DocumentDownloadResponse> GetJobOutputAsync(long jobId, CancellationToken cancellationToken = default);

        Task<IEnumerable<GetJobHostResponse>> GetJobsAsync(CancellationToken cancellationToken = default);

        Task<CreateJobHostResponse> PostJobAsync(CreateJobHostRequest createJobHostRequest, CancellationToken cancellationToken = default);

        Task<CloneJobHostResponse> PostCloneJobAsync(long jobId, CancellationToken cancellationToken = default);

        Task PutJobAsync(long jobId, CancellationToken cancellationToken = default);

        Task<GetModuleHostResponse> GetModuleAsync(long id, CancellationToken cancellationToken = default);

        Task<IEnumerable<GetPlainModuleHostResponse>> GetModulesAsync(CancellationToken cancellationToken = default);

        Task<CreateModuleHostResponse> PostModuleAsync(CreateModuleHostRequest createModuleHostRequest, CancellationToken cancellationToken = default);

        Task DeleteModuleAsync(long id, CancellationToken cancellationToken = default);
    }
}