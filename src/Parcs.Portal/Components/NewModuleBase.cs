using Microsoft.AspNetCore.Components;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class NewModuleBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }
    }
}