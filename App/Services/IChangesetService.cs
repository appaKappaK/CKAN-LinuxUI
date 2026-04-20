using System.Collections.Generic;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IChangesetService
    {
        IReadOnlyList<QueuedActionModel> CurrentQueue { get; }
    }
}
