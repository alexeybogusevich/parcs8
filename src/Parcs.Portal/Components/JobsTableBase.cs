using Microsoft.AspNetCore.Components;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Models;
using Parcs.Portal.Services.Interfaces;
using Parcs.Portal.Models.Host.Responses.Nested;
using Microsoft.JSInterop;
using Parcs.Portal.Constants;

namespace Parcs.Portal.Components
{
    public class JobsTableBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

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

        protected PaginatedList<GetJobHostResponse> CurrentPage { get; set; }

        protected List<JobStatusResponse> DisplayedStatuses { get; set; } = new ();

        protected List<JobFailureResponse> DisplayedFailures { get; set; } = new ();

        protected List<int> AvailablePages { get; set; } = new ();

        protected FiltersInput FiltersInput { get; set; } = new ();

        protected override void OnParametersSet()
        {
            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, 1, PageSize);
            SetAvailablePages();
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

        protected async Task CloneAsync()
        {
            if (JobToClone == null)
            {
                return;
            }

            IsLoading = true;

            await HostClient.PostCloneJobAsync(JobToClone.Id);

            Jobs = (await HostClient.GetJobsAsync()).ToList();

            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, CurrentPage.PageIndex, PageSize);
            SetAvailablePages();

            JobToCancel = null;

            IsLoading = false;
        }

        protected async Task CancelAsync()
        {
            if (JobToCancel == null)
            {
                return;
            }

            IsLoading = true;

            await HostClient.PutJobAsync(JobToCancel.Id);

            Jobs = (await HostClient.GetJobsAsync()).ToList();

            CurrentPage = PaginatedList<GetJobHostResponse>.Create(Jobs, CurrentPage.PageIndex, PageSize);
            SetAvailablePages();

            JobToCancel = null;

            IsLoading = false;
        }

        protected async Task DeleteAsync()
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
            DisplayedStatuses = new ();
        }

        protected async Task ShowFailuresAsync(IEnumerable<JobFailureResponse> failrues)
        {
            DisplayedFailures = failrues.ToList();
            await JsRuntime.InvokeVoidAsync(JSExtensionMethods.ToggleModal, "failures-modal");
        }

        protected void HideFailures()
        {
            DisplayedFailures = new ();
        }

        protected void DownloadOutput(long jobId)
        {
            NavigationManager.NavigateTo($"/api/jobsOutput/{jobId}", true);
        }
    }
}