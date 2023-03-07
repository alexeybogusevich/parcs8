using Parcs.Core;
using Parcs.TCP.Daemon.Handlers.Interfaces;

namespace Parcs.TCP.Daemon.Handlers
{
    internal sealed class ExecuteClassSignalHandler : ISignalHandler
    {
        public void Handle(IChannel channel)
        {
            Console.WriteLine(channel.ReadString());
            Console.WriteLine(channel.ReadDouble());
            Console.WriteLine(channel.ReadBoolean());
            Console.WriteLine(channel.ReadString());
            Console.WriteLine(channel.ReadByte());
            Console.WriteLine(channel.ReadLong());
            Console.WriteLine(channel.ReadInt());

            var job = channel.ReadObject<Job>();
            Console.WriteLine("JOB");
            Console.WriteLine(job.Id);
            Console.WriteLine(job.Status);
            Console.WriteLine(job.CreateDateUtc);
            Console.WriteLine(job.StartDateUtc);
            Console.WriteLine(job.EndDateUtc);

            channel.WriteData(1111.11D);
        }
    }
}