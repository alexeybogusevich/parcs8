namespace Parcs.Net
{
    public interface IHostInfo
    {
        int AvailablePointsNumber { get; }
        IInputReader GetInputReader();
        IOutputWriter GetOutputWriter();
        Task<IPoint> CreatePointAsync();
    }
}