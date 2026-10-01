namespace Parcs.Daemon.Exceptions
{
    /// <summary>
    /// Thrown when this daemon pod receives a point-creation message for a job it has already
    /// initialized. This happens when the Pub/Sub subscriber delivers more than one message to
    /// the same pod (see <see cref="Parcs.Daemon.HostedServices.PointCreationConsumer"/>); the
    /// message must be Nacked so Pub/Sub redelivers it to a pod that isn't already busy.
    /// </summary>
    public sealed class DuplicateJobException(long jobId) : Exception($"Job {jobId} already exists on this daemon pod.")
    {
        public long JobId { get; } = jobId;
    }
}
