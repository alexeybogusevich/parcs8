using Parcs.Net;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IInputOutputFactory
    {
        IInputReader CreateReader(Job job);
        IOutputWriter CreateWriter(Job job);
    }
}