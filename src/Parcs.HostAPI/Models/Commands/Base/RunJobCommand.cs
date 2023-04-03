namespace Parcs.HostAPI.Models.Commands.Base
{
    public class RunJobCommand
    {
        public RunJobCommand()
        {
        }

        public RunJobCommand(Guid jobId, string jsonArgumentsDictionary, int numberOfDaemons)
        {
            JobId = jobId;
            JsonArgumentsDictionary = jsonArgumentsDictionary;
            NumberOfDaemons = numberOfDaemons;
        }

        public Guid JobId { get; set; }

        public string JsonArgumentsDictionary { get; set; }

        public int NumberOfDaemons { get; set; }
    }
}