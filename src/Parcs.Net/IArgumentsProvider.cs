namespace Parcs.Net
{
    public interface IArgumentsProvider
    {
        T Bind<T>() where T : class, new();

        bool TryGet(string key, out string value);
    }
}