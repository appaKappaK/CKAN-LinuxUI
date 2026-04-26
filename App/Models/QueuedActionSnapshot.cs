using System.Collections.Generic;
using System.Linq;

namespace CKAN.App.Models
{
    public sealed class QueuedActionSnapshot
    {
        public string InstanceName { get; set; } = "";

        public List<QueuedActionSnapshotItem> Actions { get; set; } = new List<QueuedActionSnapshotItem>();

        public QueuedActionSnapshot Clone()
            => new QueuedActionSnapshot
            {
                InstanceName = InstanceName,
                Actions = (Actions ?? new List<QueuedActionSnapshotItem>())
                    .Select(item => item.Clone())
                    .ToList(),
            };
    }

    public sealed class QueuedActionSnapshotItem
    {
        public string Identifier { get; set; } = "";

        public string Name { get; set; } = "";

        public string TargetVersion { get; set; } = "";

        public QueuedActionKind ActionKind { get; set; }

        public string ActionText { get; set; } = "";

        public string DetailText { get; set; } = "";

        public string SourceText { get; set; } = "";

        public QueuedActionSnapshotItem Clone()
            => new QueuedActionSnapshotItem
            {
                Identifier    = Identifier,
                Name          = Name,
                TargetVersion = TargetVersion,
                ActionKind    = ActionKind,
                ActionText    = ActionText,
                DetailText    = DetailText,
                SourceText    = SourceText,
            };
    }
}
