using Microsoft.AspNetCore.Components;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class ViewModuleBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        [Parameter]
        public long Id { get; set; }

        protected GetModuleHostResponse Module { get; set; } = new ();

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;

            Module = await HostClient.GetModuleAsync(Id, cancellationTokenSource.Token);

            IsLoading = false;
        }
    }
}