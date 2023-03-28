using Parcs.Daemon.Models;
using Parcs.Daemon.Services.Interfaces;

namespace Parcs.Daemon.Services
{
    public class JobContextAccessor : IJobContextAccessor
    {
        public JobContext Current { get; private set; }

        public void Set(Guid jobId, string workerModulesPath)
        {
            Current = new JobContext(jobId, workerModulesPath);
        }

        public void Reset()
        {
            Current = null;
        }
    }
}