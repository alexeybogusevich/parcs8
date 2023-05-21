using Microsoft.AspNetCore.Components;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class JobsBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        public List<GetJobHostResponse> JobsList { get; set; }

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;

            var modules = await HostClient.GetJobsAsync(cancellationTokenSource.Token);
            JobsList = modules.ToList();

            IsLoading = false;
        }
    }
}