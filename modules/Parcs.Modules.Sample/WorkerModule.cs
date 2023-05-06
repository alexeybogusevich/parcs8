using Parcs.Modules.Sample.Models;
using Parcs.Net;

namespace Parcs.Modules.Sample
{
    public class WorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
            throw new ApplicationException("End of the world");

            Console.WriteLine(await moduleInfo.Parent.ReadDoubleAsync());
            Console.WriteLine(await moduleInfo.Parent.ReadBooleanAsync());
            Console.WriteLine(await moduleInfo.Parent.ReadStringAsync());
            Console.WriteLine(await moduleInfo.Parent.ReadByteAsync());
            Console.WriteLine(string.Join(' ', await moduleInfo.Parent.ReadBytesAsync()));
            Console.WriteLine(await moduleInfo.Parent.ReadLongAsync());
            Console.WriteLine(await moduleInfo.Parent.ReadIntAsync());

            var sampleClass = await moduleInfo.Parent.ReadObjectAsync<SampleClass>();

            Console.WriteLine(sampleClass.Id);
            Console.WriteLine(sampleClass.Name);

            var runRecursively = await moduleInfo.Parent.ReadBooleanAsync();

            if (runRecursively)
            {
                var point = await moduleInfo.CreatePointAsync();
                var channel = await point.CreateChannelAsync();
                await point.ExecuteClassAsync<WorkerModule>();

                await channel.WriteDataAsync(10.1D);
                await channel.WriteDataAsync(true);
                await channel.WriteDataAsync("Hello world");
                await channel.WriteDataAsync((byte)1);
                await channel.WriteDataAsync(new byte[] { 1, 0, 1 });
                await channel.WriteDataAsync(123L);
                await channel.WriteDataAsync(22);
                await channel.WriteObjectAsync(new SampleClass { Id = Guid.NewGuid(), Name = "Test" });

                await channel.WriteDataAsync(false);

                var result = await channel.ReadDoubleAsync();

                Console.WriteLine($"This worker got from another worker recursively: {result}");
            }

            await moduleInfo.Parent.WriteDataAsync(1111.11D);
        }
    }
}