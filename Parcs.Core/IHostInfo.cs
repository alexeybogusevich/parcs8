namespace Parcs.Net
{
    public interface IHostInfo
    {
        int AvailablePointsNumber { get; }
        Task<IPoint> CreatePointAsync();
    }
}