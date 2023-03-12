using Parcs.Core;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.TCP.Daemon.Handlers
{
    internal sealed class ExecuteClassSignalHandler : ISignalHandler
    {
        public async Task HandleAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            Console.WriteLine(await channel.ReadStringAsync(cancellationToken));
            Console.WriteLine(await channel.ReadDoubleAsync(cancellationToken));
            Console.WriteLine(await channel.ReadBooleanAsync(cancellationToken));
            Console.WriteLine(await channel.ReadStringAsync(cancellationToken));
            Console.WriteLine(await channel.ReadByteAsync(cancellationToken));
            Console.WriteLine(await channel.ReadLongAsync(cancellationToken));
            Console.WriteLine(await channel.ReadIntAsync(cancellationToken));

            var job = await channel.ReadObjectAsync<Job>(cancellationToken);
            Console.WriteLine("JOB");
            Console.WriteLine(job.Id);
            Console.WriteLine(job.Status);
            Console.WriteLine(job.CreateDateUtc);
            Console.WriteLine(job.StartDateUtc);
            Console.WriteLine(job.EndDateUtc);

            await channel.WriteDataAsync(1111.11D, cancellationToken);
        }
    }
}