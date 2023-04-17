using Parcs.Modules.Sample.Models;
using Parcs.Net;

namespace Parcs.Modules.Sample
{
    public class WorkerModule : IModule
    {
        public async Task RunAsync(IModuleInfo moduleInfo, CancellationToken cancellationToken = default)
        {
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

            await moduleInfo.Parent.WriteDataAsync(1111.11D);
        }
    }
}