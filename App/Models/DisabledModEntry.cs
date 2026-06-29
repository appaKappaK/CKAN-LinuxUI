using System;
using System.Collections.Generic;

namespace CKAN.App.Models
{
    public sealed class DisabledModEntry
    {
        public string Identifier { get; init; } = "";

        public string Name { get; init; } = "";

        public string Version { get; init; } = "";

        public string StorageDirectory { get; init; } = "";

        public DateTime? DisabledAtUtc { get; init; }

        public IReadOnlyList<string> RelativeFiles { get; init; } = Array.Empty<string>();
    }
}
