using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Parcs.Portal.Constants;
using Parcs.Portal.Models;
using Parcs.Portal.Models.Host.Requests;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class NewJobBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        [Parameter]
        public long ModuleId { get; set; } 

        protected CreateJobViewModel CreateJobViewModel { get; set; } = new ();

        protected async Task CreateJobAsync()
        {
            IsLoading = true;

            var createJobRequest = new CreateJobHostRequest
            {
                ClassName = CreateJobViewModel.ClassName,
                AssemblyName = CreateJobViewModel.AssemblyName,
                InputFiles = CreateJobViewModel.InputFiles ?? Enumerable.Empty<IBrowserFile>(),
                ModuleId = ModuleId,
            };

            await HostClient.PostJobAsync(createJobRequest, cancellationTokenSource.Token);

            IsLoading = false;

            await JsRuntime.InvokeVoidAsync(JSExtensionMethods.BackToPreviousPage);
        }

        protected void OnFileChanged(InputFileChangeEventArgs e)
        {
            CreateJobViewModel.InputFiles = e.GetMultipleFiles();
        }
    }
}