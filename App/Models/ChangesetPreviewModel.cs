using System.Collections.Generic;

namespace CKAN.App.Models
{
    public sealed class ChangesetPreviewModel
    {
        public string SummaryText { get; init; } = "";

        public bool CanApply { get; init; }

        public IReadOnlyList<string> DownloadsRequired { get; init; } = new List<string>();

        public IReadOnlyList<string> DependencyInstalls { get; init; } = new List<string>();

        public IReadOnlyList<string> AutoRemovals { get; init; } = new List<string>();

        public IReadOnlyList<string> AttentionNotes { get; init; } = new List<string>();

        public IReadOnlyList<string> Recommendations { get; init; } = new List<string>();

        public IReadOnlyList<string> Suggestions { get; init; } = new List<string>();

        public IReadOnlyList<string> Supporters { get; init; } = new List<string>();

        public IReadOnlyList<string> Conflicts { get; init; } = new List<string>();
    }
}
