using Microsoft.AspNetCore.Components;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class ModulesBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        public List<GetModuleHostResponse> ModulesList { get; set; }

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;

            var modules = await HostClient.GetModulesAsync(cancellationTokenSource.Token);
            ModulesList = modules.ToList();

            IsLoading = false;
        }
    }
}