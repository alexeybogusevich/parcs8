namespace Parcs.Net
{
    public interface IHostInfo : IAsyncDisposable
    {
        int CanCreatePointsNumber { get; }
        IInputReader GetInputReader();
        IOutputWriter GetOutputWriter();
        Task<IPoint> CreatePointAsync();
    }
}