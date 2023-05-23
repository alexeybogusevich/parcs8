using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Parcs.Portal.Constants;
using Parcs.Portal.Models;
using Parcs.Portal.Models.Host.Requests;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class NewJobBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        [Parameter]
        public long ModuleId { get; set; } 

        protected GetModuleHostResponse Module { get; set; }

        protected List<string> CurrentAssemblyImplementations { get; set; } = new();

        protected CreateJobViewModel CreateJobViewModel { get; set; } = new ();

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;

            Module = await HostClient.GetModuleAsync(ModuleId, cancellationTokenSource.Token);

            IsLoading = false;
        }

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

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await JsRuntime.InvokeVoidAsync(JSExtensionMethods.SetSelect2);

            var dotNetReference = DotNetObjectReference.Create(this);

            await JsRuntime.InvokeVoidAsync(
                JSExtensionMethods.SetOnChangeSelect2, "select-class", dotNetReference, JSInvokableMethods.ChangeClass);

            await JsRuntime.InvokeVoidAsync(
                JSExtensionMethods.SetOnChangeSelect2, "select-assembly", dotNetReference, JSInvokableMethods.ChangeAssembly);
        }
    }
}