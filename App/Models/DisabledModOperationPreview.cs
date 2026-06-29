using System;
using System.Collections.Generic;

namespace CKAN.App.Models
{
    public sealed class DisabledModOperationPreview
    {
        public bool CanApply { get; init; }

        public string Title { get; init; } = "";

        public string Message { get; init; } = "";

        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> FollowUpLines { get; init; } = Array.Empty<string>();
    }
}
