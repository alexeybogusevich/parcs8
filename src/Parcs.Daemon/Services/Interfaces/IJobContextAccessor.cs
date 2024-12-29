using Parcs.Daemon.Models;

namespace Parcs.Daemon.Services.Interfaces
{
    public interface IJobContextAccessor
    {
        bool TryGet(long jobId, out JobContext jobContext);

        void Add(long jobId, long moduleId, IDictionary<string, string> arguments);

        void Remove(long jobId);
    }
}