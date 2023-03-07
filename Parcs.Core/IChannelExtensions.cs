namespace Parcs.Core
{
    public static class IChannelExtensions
    {
        public static void ExecuteClass(this IChannel channel, string className)
        {
            channel.WriteSignal(Signal.ExecuteClass);
            channel.WriteData(className);
        }
    }
}