namespace Parcs.Core
{
    public interface IHostInfo
    {
        int MaximumPointsNumber { get; }
        IPoint CreatePoint();
    }
}