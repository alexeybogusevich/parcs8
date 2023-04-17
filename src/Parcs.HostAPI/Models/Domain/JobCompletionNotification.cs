using Parcs.Core.Models;

namespace Parcs.HostAPI.Models.Domain
{
    public sealed class JobCompletionNotification
    {
        public JobCompletionNotification(Job job)
        {
            JobId = job.Id;
            JobStatus = job.Status;
            ErrorMessage = job.ErrorMessage;
            ElapsedMilliseconds = job.ExecutionTime?.Milliseconds;
        }

        public Guid JobId { get; set; }

        public JobStatus JobStatus { get; set; }

        public string ErrorMessage { get; set; }

        public int? ElapsedMilliseconds { get; set; }
    }
}