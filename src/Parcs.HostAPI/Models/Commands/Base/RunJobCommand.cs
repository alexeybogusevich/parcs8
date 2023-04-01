namespace Parcs.HostAPI.Models.Commands.Base
{
    public class RunJobCommand
    {
        public RunJobCommand()
        {
        }

        public RunJobCommand(Guid jobId, string argumentsJsonDictionary, int? numberOfDaemons)
        {
            JobId = jobId;
            ArgumentsJsonDictionary = argumentsJsonDictionary;
            NumberOfDaemons = numberOfDaemons;
        }

        public Guid JobId { get; set; }

        public string ArgumentsJsonDictionary { get; set; }

        public int? NumberOfDaemons { get; set; }
    }
}