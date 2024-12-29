using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Parcs.Portal.Configuration;
using Parcs.Portal.Constants;
using Parcs.Portal.Models;
using Parcs.Portal.Models.Host;
using Parcs.Portal.Models.Host.Requests;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class RunJobBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        protected Dictionary<string, List<string>> HostErrors { get; set; } = [];

        [Inject]
        protected IOptions<PortalConfiguration> PortalOptions { get; set; }

        [Parameter]
        public long JobId { get; set; }

        public GetJobHostResponse JobHostResponse { get; set; }

        protected RunJobViewModel RunJobViewModel { get; set; } = new ();

        protected string NewArgumentKey { get; set; }

        protected string NewArgumentValue { get; set; }

        protected override async Task OnInitializedAsync()
        {
            IsLoading = true;

            JobHostResponse = await HostClient.GetJobAsync(JobId, cancellationTokenSource.Token);

            IsLoading = false;
        }

        protected async Task RunJobAsync()
        {
            IsLoading = true;

            var runJobRequest = new RunJobHostRequest
            {
                JobId = JobId,
                Arguments = RunJobViewModel.Arguments.DistinctBy(a => a.Key).ToDictionary(a => a.Key, a => a.Value),
                CallbackUrl = string.Format($"http://{PortalOptions.Value.Uri}/{PortalOptions.Value.JobCompletionEndpoint}", JobId),
            };

            if (string.IsNullOrWhiteSpace(NewArgumentKey) is false &&
                string.IsNullOrWhiteSpace(NewArgumentValue) is false &&
                runJobRequest.Arguments.ContainsKey(NewArgumentKey) is false)
            {
                runJobRequest.Arguments.Add(NewArgumentKey, NewArgumentValue);
            }

            try
            {
                await HostClient.PostJobRunAsync(runJobRequest, cancellationTokenSource.Token);

                HostErrors.Clear();

                await JsRuntime.InvokeVoidAsync(JSExtensionMethods.BackToPreviousPage);
            }
            catch (HostException ex)
            {
                HostErrors = ex.ProblemDetails.Errors;
            }
            catch
            {
                HostErrors = new Dictionary<string, List<string>>()
                {
                    { "Error", new List<string> { "An error occurred while communicating with the Host." } }
                };
            }

            IsLoading = false;
        }

        protected void AddNewArgument()
        {
            if (string.IsNullOrWhiteSpace(NewArgumentKey) || string.IsNullOrWhiteSpace(NewArgumentValue))
            {
                return;
            }

            RunJobViewModel.Arguments.Add(new(NewArgumentKey, NewArgumentValue));
            NewArgumentKey = string.Empty;
            NewArgumentValue = string.Empty;
        }
    }
}