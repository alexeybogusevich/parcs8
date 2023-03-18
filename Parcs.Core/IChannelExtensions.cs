namespace Parcs.Core
{
    public static class IChannelExtensions
    {
        public static async Task ExecuteClassAsync(this IChannel channel, string assemblyName, string className)
        {
            await channel.WriteSignalAsync(Signal.ExecuteClass);
            await channel.WriteDataAsync(assemblyName);
            await channel.WriteDataAsync(className);
        }
    }
}