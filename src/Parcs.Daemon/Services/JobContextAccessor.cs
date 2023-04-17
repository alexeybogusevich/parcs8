using Parcs.Daemon.Models;
using Parcs.Daemon.Services.Interfaces;

namespace Parcs.Daemon.Services
{
    public class JobContextAccessor : IJobContextAccessor
    {
        public JobContext Current { get; private set; }

        public void Set(Guid jobId, Guid moduleId, int pointsNumber, IDictionary<string, string> arguments)
        {
            Current = new JobContext(jobId, moduleId, pointsNumber, arguments);
        }

        public void Reset()
        {
            Current = null;
        }
    }
}