using System.Collections.Generic;

namespace CKAN.LinuxGUI
{
    public sealed class ModRelationshipItem
    {
        public string Text { get; init; } = "";

        public IReadOnlyList<string> Identifiers { get; init; } = System.Array.Empty<string>();
    }
}
