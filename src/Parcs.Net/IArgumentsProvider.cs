namespace Parcs.Net
{
    public interface IArgumentsProvider
    {
        IDictionary<string, string> GetArguments();
    }
}