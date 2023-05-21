using Microsoft.AspNetCore.Components;
using Parcs.Portal.Models;
using Parcs.Portal.Models.Host.Responses;
using Parcs.Portal.Services.Interfaces;

namespace Parcs.Portal.Components
{
    public class ModulesTableBase : PageBase
    {
        [Inject]
        protected IHostClient HostClient { get; set; }

        protected int Counter = 1;

        private readonly int PageSize = 10;

        private readonly int PagesForReference = 5;

        [Parameter]
        public List<GetModuleHostResponse> Modules { get; set; }

        protected GetModuleHostResponse ModuleToDelete { get; set; }

        protected PaginatedList<GetModuleHostResponse> CurrentPage { get; set; }

        protected List<int> AvailablePages { get; set; } = new ();

        protected FiltersInput FiltersInput { get; set; } = new ();

        protected override void OnParametersSet()
        {
            CurrentPage = PaginatedList<GetModuleHostResponse>.Create(Modules, 1, PageSize);
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
            CurrentPage = PaginatedList<GetModuleHostResponse>.Create(Modules, pageNumber, PageSize);
            SetAvailablePages();
        }

        protected void Filter()
        {
            if (string.IsNullOrEmpty(FiltersInput.SearchWord))
            {
                CurrentPage = PaginatedList<GetModuleHostResponse>.Create(Modules, 1, PageSize);
                SetAvailablePages();
                return;
            }

            var filteredModules = Modules.Where(p => p.Name.Contains(FiltersInput.SearchWord, StringComparison.OrdinalIgnoreCase));

            CurrentPage = PaginatedList<GetModuleHostResponse>.Create(filteredModules, 1, PageSize);

            SetAvailablePages();
        }

        protected void ClearFilters()
        {
            FiltersInput.SearchWord = string.Empty;
            CurrentPage = PaginatedList<GetModuleHostResponse>.Create(Modules, 1, PageSize);
            SetAvailablePages();
        }

        protected void SetModuleToDelete(GetModuleHostResponse module)
        {
            ModuleToDelete = module;
        }

        protected void ResetModuleToDelete()
        {
            ModuleToDelete = null;
        }

        protected async Task DeleteAsync()
        {
            if (ModuleToDelete == null)
            {
                return;
            }

            await HostClient.DeleteModuleAsync(ModuleToDelete.Id);

            var deletedDoctor = Modules.FirstOrDefault(d => d.Id.Equals(ModuleToDelete.Id));
            Modules.Remove(deletedDoctor);

            CurrentPage = PaginatedList<GetModuleHostResponse>.Create(Modules, CurrentPage.PageIndex, PageSize);
            SetAvailablePages();

            ModuleToDelete = null;
        }
    }
}