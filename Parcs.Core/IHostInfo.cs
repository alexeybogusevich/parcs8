namespace Parcs.Core
{
    public interface IHostInfo
    {
        int AvailablePointsNumber { get; }
        Task<IPoint> CreatePointAsync();
    }
}