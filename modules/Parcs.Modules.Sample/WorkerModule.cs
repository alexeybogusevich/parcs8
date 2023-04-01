using Parcs.Modules.Sample.Models;
using Parcs.Net;

namespace Parcs.Modules.Sample
{
    public class WorkerModule : IWorkerModule
    {
        public string Name => "Sample worker module";

        public async Task RunAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            Console.WriteLine(await channel.ReadDoubleAsync(cancellationToken));
            Console.WriteLine(await channel.ReadBooleanAsync(cancellationToken));
            Console.WriteLine(await channel.ReadStringAsync(cancellationToken));
            Console.WriteLine(await channel.ReadByteAsync(cancellationToken));
            Console.WriteLine(await channel.ReadLongAsync(cancellationToken));
            Console.WriteLine(await channel.ReadIntAsync(cancellationToken));

            var sampleClass = await channel.ReadObjectAsync<SampleClass>(cancellationToken);

            Console.WriteLine(sampleClass.Id);
            Console.WriteLine(sampleClass.Name);

            await channel.WriteDataAsync(1111.11D, cancellationToken);
        }
    }
}