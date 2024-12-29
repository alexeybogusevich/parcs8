using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IArgumentsProviderFactory
    {
        IArgumentsProvider Create(IDictionary<string, string> arguments);
    }
}