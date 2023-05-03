using System.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IAddressResolver
    {
        IPAddress[] Resolve(string url);
    }
}