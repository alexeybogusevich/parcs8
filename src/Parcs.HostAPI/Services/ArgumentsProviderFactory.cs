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

        public IArgumentsProvider Create(string jsonArgumentsDictionary)
        {
            var arguments = _jsonDictionaryParser.Parse(jsonArgumentsDictionary);
            return new ArgumentsProvider(arguments);
        }
    }
}