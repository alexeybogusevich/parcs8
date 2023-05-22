using Microsoft.AspNetCore.Components;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class ModuleInfoBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        [Parameter]
        public GetModuleHostResponse Module { get; set; }
    }
}