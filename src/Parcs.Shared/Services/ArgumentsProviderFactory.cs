using Parcs.Net;
using Parcs.Shared.Services.Interfaces;

namespace Parcs.Shared.Services
{
    public class ArgumentsProviderFactory : IArgumentsProviderFactory
    {
        public IArgumentsProvider Create(int pointsNumber, IDictionary<string, string> arguments) =>
            new ArgumentsProvider(pointsNumber, arguments);
    }
}