using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IModSearchService
    {
        FilterState Current { get; }

        void SetCurrent(FilterState current);
    }
}
