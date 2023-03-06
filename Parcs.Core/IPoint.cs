namespace Parcs.Core
{
    public interface IPoint
    {
        IChannel CreateChannel();
        void Delete();
    }
}