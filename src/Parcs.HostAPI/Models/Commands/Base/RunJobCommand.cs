namespace Parcs.HostAPI.Models.Commands.Base
{
    public class RunJobCommand
    {
        public RunJobCommand()
        {
        }

        public RunJobCommand(Guid jobId, string jsonArgumentsDictionary)
        {
            JobId = jobId;
            JsonArgumentsDictionary = jsonArgumentsDictionary;
        }

        public Guid JobId { get; set; }

        public string JsonArgumentsDictionary { get; set; }
    }
}