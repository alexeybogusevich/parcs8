namespace Parcs.HostAPI.Models.Commands.Base
{
    public class RunJobCommand
    {
        public RunJobCommand()
        {
        }

        public RunJobCommand(Guid jobId, int pointsNumber, string rawArgumentsDictionary)
        {
            JobId = jobId;
            PointsNumber = pointsNumber;
            RawArgumentsDictionary = rawArgumentsDictionary;
        }

        public Guid JobId { get; set; }

        public int PointsNumber { get; set; }

        public string RawArgumentsDictionary { get; set; }
    }
}