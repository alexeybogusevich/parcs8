using Parcs.Daemon.Models;

namespace Parcs.Daemon.Services.Interfaces
{
    public interface IJobContextAccessor
    {
        bool TryGet(Guid jobId, out JobContext jobContext);

        void Add(Guid jobId, Guid moduleId, int pointsNumber, IDictionary<string, string> arguments);

        void Remove(Guid jobId);
    }
}