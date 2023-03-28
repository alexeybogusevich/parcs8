using Parcs.Daemon.Models;

namespace Parcs.Daemon.Services.Interfaces
{
    public interface IJobContextAccessor
    {
        JobContext Current { get; }

        void Set(Guid jobId, string workerModulesPath);

        void Reset();
    }
}