namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IJsonDictionaryParser
    {
        IDictionary<string, string> Parse(string argumentsJsonDictionary);
    }
}