namespace Parcs.Host.Models.Commands.Base
{
    public class RunJobCommand
    {
        public RunJobCommand()
        {
        }

        public RunJobCommand(long jobId, Dictionary<string, string> arguments)
        {
            JobId = jobId;
            Arguments = arguments;
        }

        public long JobId { get; set; }

        public Dictionary<string, string> Arguments { get; set; }
    }
}