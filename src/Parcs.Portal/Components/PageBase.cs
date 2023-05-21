using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Parcs.Portal.Components
{
    public class PageBase : ComponentBase, IDisposable
    {
        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        protected bool IsLoading { get; set; }

        protected readonly CancellationTokenSource cancellationTokenSource = new ();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
    }
}