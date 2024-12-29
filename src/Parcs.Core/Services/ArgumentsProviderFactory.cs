using Parcs.Net;
using Parcs.Core.Services.Interfaces;

namespace Parcs.Core.Services
{
    public class ArgumentsProviderFactory : IArgumentsProviderFactory
    {
        public IArgumentsProvider Create(IDictionary<string, string> arguments) =>
            new ArgumentsProvider(arguments);
    }
}