namespace Parcs.Net
{
    public interface IPoint
    {
        IChannel CreateChannel();
        void Delete();
    }
}