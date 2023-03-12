namespace Parcs.Core
{
    public static class IChannelExtensions
    {
        public static async Task ExecuteClassAsync(this IChannel channel, string className)
        {
            await channel.WriteSignalAsync(Signal.ExecuteClass);
            await channel.WriteDataAsync(className);
        }
    }
}