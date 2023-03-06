namespace Parcs.Core
{
    public interface IHostInfo
    {
        int MaximumPoints { get; }
        IPoint CreatePoint();
    }
}