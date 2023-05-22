using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Parcs.Portal.Constants;
using Parcs.Portal.Models;
using Parcs.Portal.Models.Host.Requests;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class NewModuleBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        protected CreateModuleViewModel CreateModuleViewModel { get; set; } = new ();

        protected async Task CreateModuleAsync()
        {
            IsLoading = true;

            var createModuleRequest = new CreateModuleHostRequest
            {
                Name = CreateModuleViewModel.Name,
                BinaryFiles = CreateModuleViewModel.BinaryFiles ?? Enumerable.Empty<IBrowserFile>(),
            };

            await HostClient.PostModuleAsync(createModuleRequest, cancellationTokenSource.Token);

            IsLoading = false;

            await JsRuntime.InvokeVoidAsync(JSExtensionMethods.BackToPreviousPage);
        }

        protected void OnFileChanged(InputFileChangeEventArgs e)
        {
            CreateModuleViewModel.BinaryFiles = e.GetMultipleFiles();
        }
    }
}