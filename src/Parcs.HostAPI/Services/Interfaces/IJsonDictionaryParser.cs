namespace Parcs.HostAPI.Services.Interfaces
{
    public interface IJsonDictionaryParser
    {
        IReadOnlyDictionary<string, string> Parse(string argumentsJsonDictionary);
    }
}