using Parcs.Net;

namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IArgumentsProviderFactory
    {
        IArgumentsProvider Create(string jsonArgumentsDictionary);
    }
}