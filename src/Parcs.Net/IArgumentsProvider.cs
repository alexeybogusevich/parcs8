namespace Parcs.Net
{
    public interface IArgumentsProvider
    {
        int GetPointsNumber();

        IDictionary<string, string> GetArguments();
    }
}