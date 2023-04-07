using Parcs.Net;
using Parcs.Shared.Models;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IHostInfoFactory
    {
        Task<IHostInfo> CreateAsync(Job job);
    }
}