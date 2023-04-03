using Parcs.HostAPI.Services.Interfaces;
using System.Text.Json;

namespace Parcs.HostAPI.Services
{
    public class JsonDictionaryParser : IJsonDictionaryParser
    {
        public IDictionary<string, string> Parse(string argumentsJsonDictionary)
        {
            if (string.IsNullOrWhiteSpace(argumentsJsonDictionary))
            {
                return new Dictionary<string, string>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(argumentsJsonDictionary);
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }
    }
}