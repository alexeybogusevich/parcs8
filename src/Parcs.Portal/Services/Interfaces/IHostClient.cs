using LanguageExt.Common;
using Parcs.Portal.Models.Host.Requests;
using Parcs.Portal.Models.Host.Responses;

namespace Parcs.Portal.Services.Interfaces
{
    public interface IHostClient
    {
        Task<Result<bool>> PostRunAsync(RunJobHostRequest runJobHostRequest, CancellationToken cancellationToken = default);

        Task<GetJobHostResponse> GetJobAsync(long jobId, CancellationToken cancellationToken = default);

        Task<IEnumerable<GetJobHostResponse>> GetJobsAsync(long moduleId, CancellationToken cancellationToken = default);

        Task<Result<CreateJobHostResponse>> PostJobAsync(CreateJobHostRequest createJobHostRequest, CancellationToken cancellationToken = default);

        Task<GetModuleHostResponse> GetModuleAsync(long id, CancellationToken cancellationToken = default);

        Task<IEnumerable<GetModuleHostResponse>> GetModulesAsync(CancellationToken cancellationToken = default);

        Task<Result<CreateModuleHostResponse>> PostModuleAsync(CreateModuleHostRequest createModuleHostRequest, CancellationToken cancellationToken = default);

        Task DeleteModuleAsync(long id, CancellationToken cancellationToken = default);
    }
}