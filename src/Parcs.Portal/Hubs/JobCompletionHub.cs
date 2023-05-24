using Microsoft.AspNetCore.SignalR;
using Parcs.Portal.Constants;

namespace Parcs.Portal.Hubs
{
    public class JobCompletionHub : Hub
    {
        public async Task NotifyCompletion(long jobId)
        {
            await Clients.All.SendAsync(JobCompletionHubMethods.NotifyCompletion, jobId);
        }
    }
}