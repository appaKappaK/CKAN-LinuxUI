using System;
using System.Collections.Generic;

namespace CKAN.App.Models
{
    public sealed class DisabledModsSnapshot
    {
        public string? DisabledDirectoryPath { get; init; }

        public IReadOnlyDictionary<string, DisabledModEntry> Entries { get; init; }
            = new Dictionary<string, DisabledModEntry>(StringComparer.OrdinalIgnoreCase);

        public bool HasDisabledDirectory
            => !string.IsNullOrWhiteSpace(DisabledDirectoryPath);

        public bool IsDisabled(string identifier)
            => !string.IsNullOrWhiteSpace(identifier)
               && Entries.ContainsKey(identifier);
    }
}
