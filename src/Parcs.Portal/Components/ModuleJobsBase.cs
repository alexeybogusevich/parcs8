using Microsoft.AspNetCore.Components;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class ModuleJobsBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        [Parameter]
        public Guid ModuleId { get; set; }

        [Parameter]
        public List<GetJobHostResponse> Jobs { get; set; }
    }
}