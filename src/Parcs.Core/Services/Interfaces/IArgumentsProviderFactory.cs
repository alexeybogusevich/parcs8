using Parcs.Net;

namespace Parcs.Core.Services.Interfaces
{
    public interface IArgumentsProviderFactory
    {
        IArgumentsProvider Create(int pointsNumber, IDictionary<string, string> arguments);
    }
}