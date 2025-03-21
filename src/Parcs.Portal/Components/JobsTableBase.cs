﻿using Microsoft.AspNetCore.Components;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Models;
using Parcs.Portal.Services.Interfaces;
using Parcs.Portal.Models.Host.Responses.Nested;
using Microsoft.JSInterop;
using Parcs.Portal.Constants;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Parcs.Portal.Configuration;

namespace Parcs.Portal.Components
{
    public class JobsTableBase : PageBase, IAsyncDisposable
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        [Inject]
        protected IOptions<PortalConfiguration> PortalOptions { get; set; }

        protected HubConnection HubConnection { get; set; }

        protected int Counter = 1;

        private readonly int PageSize = 10;

        private readonly int PagesForReference = 5;

        [Parameter]
        public List<GetJobHostResponse> Jobs { get; set; }

        [Parameter]
        public long? ModuleId { get; set; }

        protected GetJobHostResponse JobToDelete { get; set; }

        protected GetJobHostResponse JobToCancel { get; set; }

        protected GetJobHostResponse JobToClone { get; set; }

        protected GetJobHostResponse JobToRun { get; set; }

        protected PaginatedList<GetJobHostResponse> CurrentPage { get; set; }

        protected List<JobStatusResponse> DisplayedStatuses { get; set; } = [];

        protected List<JobFailureResponse> DisplayedFailures { get; set; } = [];

        protected List<int> AvailablePages { get; set; } = [];

        protected FiltersInput FiltersInput { get; set; } = new ();

        protected override async Task OnInitializedAsync()
        {
            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, 1, PageSize);

            SetAvailablePages();

            HubConnection = new HubConnectionBuilder()
                .WithUrl($"http://{PortalOptions.Value.SignalrUri}/jobCompletionHub")
                .Build();

            HubConnection.On<long>(JobCompletionHubMethods.NotifyCompletion, (jobId) =>
            {
                InvokeAsync(async () => await HandleJobCompletionAsync(jobId));
            });

            await HubConnection.StartAsync();
        }

        protected void SetAvailablePages()
        {
            var resultPages = new List<int>();

            if (CurrentPage.HasPreviousPage)
            {
                resultPages.Add(CurrentPage.PageIndex - 1);
            }

            for (int i = CurrentPage.PageIndex; i <= CurrentPage.TotalPages && resultPages.Count < PagesForReference; ++i)
            {
                resultPages.Add(i);
            }

            AvailablePages = resultPages;
        }

        protected void GoToPage(int pageNumber)
        {
            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, pageNumber, PageSize);
            SetAvailablePages();
        }

        protected void Filter()
        {
            if (string.IsNullOrEmpty(FiltersInput.SearchWord))
            {
                CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, 1, PageSize);
                SetAvailablePages();
                return;
            }

            var filteredJobs = Jobs.Where(p =>
                p.AssemblyName.Contains(FiltersInput.SearchWord, StringComparison.OrdinalIgnoreCase) ||
                p.ClassName.Contains(FiltersInput.SearchWord, StringComparison.OrdinalIgnoreCase) ||
                p.ModuleName.Contains(FiltersInput.SearchWord, StringComparison.OrdinalIgnoreCase));

            CurrentPage = PaginatedList<GetJobHostResponse>.Create(filteredJobs, 1, PageSize);

            SetAvailablePages();
        }

        protected void ClearFilters()
        {
            FiltersInput.SearchWord = string.Empty;
            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, 1, PageSize);
            SetAvailablePages();
        }

        protected void SetJobToDelete(GetJobHostResponse job)
        {
            JobToDelete = job;
        }

        protected void ResetJobToDelete()
        {
            JobToDelete = null;
        }

        protected void SetJobToCancel(GetJobHostResponse job)
        {
            JobToCancel = job;
        }

        protected void ResetJobToCancel()
        {
            JobToCancel = null;
        }

        protected void SetJobToClone(GetJobHostResponse job)
        {
            JobToClone = job;
        }

        protected void ResetJobToClone()
        {
            JobToClone = null;
        }

        protected void SetJobToRun(GetJobHostResponse job)
        {
            JobToRun = job;
        }

        protected void ResetJobToRun()
        {
            JobToRun = null;
        }

        protected void RunJob()
        {
            if (JobToRun == null)
            {
                return;
            }

            NavigationManager.NavigateTo($"jobs/{JobToRun.Id}/start", true);
        }

        protected async Task CloneJobAsync()
        {
            if (JobToClone == null)
            {
                return;
            }

            IsLoading = true;

            await HostClient.PostCloneJobAsync(JobToClone.Id);

            if (ModuleId is long moduleId)
            {
                var module = await HostClient.GetModuleAsync(moduleId, cancellationTokenSource.Token);
                Jobs = module.Jobs.ToList();
            }
            else
            {
                Jobs = (await HostClient.GetJobsAsync()).ToList();
            }

            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, CurrentPage.PageIndex, PageSize);
            SetAvailablePages();

            JobToCancel = null;

            IsLoading = false;
        }

        protected async Task CancelJobAsync()
        {
            if (JobToCancel == null)
            {
                return;
            }

            IsLoading = true;

            await HostClient.PutJobAsync(JobToCancel.Id);

            if (ModuleId is long moduleId)
            {
                var module = await HostClient.GetModuleAsync(moduleId, cancellationTokenSource.Token);
                Jobs = module.Jobs.ToList();
            }
            else
            {
                Jobs = (await HostClient.GetJobsAsync()).ToList();
            }

            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, CurrentPage.PageIndex, PageSize);
            SetAvailablePages();

            JobToCancel = null;

            IsLoading = false;
        }

        protected async Task DeleteJobAsync()
        {
            if (JobToDelete == null)
            {
                return;
            }

            await HostClient.DeleteModuleAsync(JobToDelete.Id);

            var deletedJob = Jobs.FirstOrDefault(d => d.Id.Equals(JobToDelete.Id));
            Jobs.Remove(deletedJob);

            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, CurrentPage.PageIndex, PageSize);
            SetAvailablePages();

            JobToDelete = null;
        }

        protected async Task ShowStatusesAsync(IEnumerable<JobStatusResponse> statuses)
        {
            DisplayedStatuses = statuses.ToList();
            await JsRuntime.InvokeVoidAsync(JSExtensionMethods.ToggleModal, "statuses-modal");
        }

        protected void HideStatuses()
        {
            DisplayedStatuses = [];
        }

        protected async Task ShowFailuresAsync(IEnumerable<JobFailureResponse> failrues)
        {
            DisplayedFailures = failrues.ToList();
            await JsRuntime.InvokeVoidAsync(JSExtensionMethods.ToggleModal, "failures-modal");
        }

        protected void HideFailures()
        {
            DisplayedFailures = [];
        }

        protected void DownloadOutput(long jobId)
        {
            NavigationManager.NavigateTo($"/api/jobsOutput/{jobId}", true);
        }

        private async Task HandleJobCompletionAsync(long jobId)
        {
            var oldJob = Jobs.FirstOrDefault(d => d.Id.Equals(jobId));

            if (oldJob is null)
            {
                return;
            }

            var newJob = await HostClient.GetJobAsync(jobId, cancellationTokenSource.Token);

            Jobs.Remove(oldJob);
            Jobs.Add(newJob);

            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs.OrderByDescending(j => j.Id), CurrentPage.PageIndex, PageSize);
            SetAvailablePages();

            StateHasChanged();
        }

        public async ValueTask DisposeAsync()
        {
            if (HubConnection is not null)
            {
                await HubConnection.DisposeAsync();
            }

            GC.SuppressFinalize(this);
        }
    }
}