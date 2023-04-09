using Parcs.HostAPI.Services.Interfaces;
using Parcs.Net;

namespace Parcs.HostAPI.Services
{
    public class ArgumentsProviderFactory : IArgumentsProviderFactory
    {
        private readonly IJsonDictionaryParser _jsonDictionaryParser;

        public ArgumentsProviderFactory(IJsonDictionaryParser jsonDictionaryParser)
        {
            _jsonDictionaryParser = jsonDictionaryParser;
        }

        public IArgumentsProvider Create(int pointsNumber, string rawArgumentsDictionary)
        {
            var parsedArguments = _jsonDictionaryParser.Parse(rawArgumentsDictionary);
            return new ArgumentsProvider(pointsNumber, parsedArguments);
        }
    }
}