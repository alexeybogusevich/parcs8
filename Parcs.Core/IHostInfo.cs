namespace Parcs.Core
{
    public interface IHostInfo
    {
        int MaximumPointsNumber { get; }
        Task<IPoint> CreatePointAsync();
    }
}