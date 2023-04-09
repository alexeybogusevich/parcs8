namespace Parcs.Net
{
    public interface IArgumentsProvider
    {
        ArgumentsBase GetBase();

        IDictionary<string, string> GetRaw();
    }
}