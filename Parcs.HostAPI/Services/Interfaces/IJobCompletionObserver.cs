using Parcs.Core;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IJobCompletionObserver
    {
        void Subscribe(Job job);
    }
}