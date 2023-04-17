using Parcs.Daemon.Models;

namespace Parcs.Daemon.Services.Interfaces
{
    public interface IJobContextAccessor
    {
        JobContext Current { get; }

        void Set(Guid jobId, Guid moduleId, int pointsNumber, IDictionary<string, string> arguments);

        void Reset();
    }
}