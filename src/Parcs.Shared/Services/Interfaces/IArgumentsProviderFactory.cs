using Parcs.Net;

namespace Parcs.Shared.Services.Interfaces
{
    public interface IArgumentsProviderFactory
    {
        IArgumentsProvider Create(int pointsNumber, IDictionary<string, string> arguments);
    }
}