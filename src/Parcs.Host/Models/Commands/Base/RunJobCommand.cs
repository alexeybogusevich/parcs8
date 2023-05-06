namespace Parcs.Host.Models.Commands.Base
{
    public class RunJobCommand
    {
        public RunJobCommand()
        {
        }

        public RunJobCommand(long jobId, int pointsNumber, Dictionary<string, string> arguments)
        {
            JobId = jobId;
            PointsNumber = pointsNumber;
            Arguments = arguments;
        }

        public long JobId { get; set; }

        public int PointsNumber { get; set; }

        public Dictionary<string, string> Arguments { get; set; }
    }
}