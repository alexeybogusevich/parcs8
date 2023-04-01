using Parcs.HostAPI.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Parcs.HostAPI.Services
{
    public class JsonDictionaryParser : IJsonDictionaryParser
    {
        public IReadOnlyDictionary<string, string> Parse(string argumentsJsonDictionary)
        {
            if (string.IsNullOrWhiteSpace(argumentsJsonDictionary))
            {
                return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            }

            try
            {
                var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(argumentsJsonDictionary);
                return new ReadOnlyDictionary<string, string>(dictionary);
            }
            catch
            {
                return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            }
        }
    }
}