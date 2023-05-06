using System.Text.Json;

namespace Parcs.Host.Models.Commands.Base
{
    public class RunJobCommand
    {
        public RunJobCommand()
        {
        }

        public RunJobCommand(long jobId, int pointsNumber, string rawArgumentsDictionary)
        {
            JobId = jobId;
            PointsNumber = pointsNumber;
            RawArgumentsDictionary = rawArgumentsDictionary;
        }

        public long JobId { get; set; }

        public int PointsNumber { get; set; }

        public string RawArgumentsDictionary { get; set; }

        public Dictionary<string, string> GetArgumentsDictionary()
        {
            if (string.IsNullOrWhiteSpace(RawArgumentsDictionary))
            {
                return new Dictionary<string, string>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(RawArgumentsDictionary);
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }
    }
}