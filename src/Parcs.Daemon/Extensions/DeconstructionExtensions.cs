using Parcs.Daemon.Models;

namespace Parcs.Daemon.Extensions
{
    public static class DeconstructionExtensions
    {
        public static void Deconstruct(
            this JobContext jobContext,
            out Guid jobId,
            out Guid moduleId,
            out int pointsNumber,
            out IDictionary<string, string> arguments,
            out CancellationToken cancellationToken)
        {
            jobId = jobContext.JobId;
            moduleId = jobContext.ModuleId;
            pointsNumber = jobContext.PointsNumber;
            arguments = jobContext.Arguments;
            cancellationToken = jobContext.CancellationTokenSource.Token;
        }
    }
}