using System.Collections.Generic;
using System.Linq;

using CKAN.Versioning;

namespace CKAN.LinuxGUI
{
    public static class CompatibilityVersionOptionBuilder
    {
        public static IReadOnlyList<CompatibilityVersionOption> Build(GameInstance instance)
        {
            var currentVersion = instance.Version();
            var compatibleVersions = instance.CompatibleVersions;
            var knownVersions = instance.Game.KnownVersions;
            var majorVersions = knownVersions.Select(v => new GameVersion(v.Major, v.Minor))
                                             .Distinct()
                                             .ToArray();
            var seen = new HashSet<GameVersion>();
            var options = new List<CompatibilityVersionOption>();

            AddOptions(options,
                       compatibleVersions.Except(knownVersions)
                                         .Except(majorVersions),
                       compatibleVersions,
                       currentVersion,
                       seen);
            AddOptions(options,
                       majorVersions,
                       compatibleVersions,
                       currentVersion,
                       seen);
            AddOptions(options,
                       knownVersions,
                       compatibleVersions,
                       currentVersion,
                       seen);

            return options;
        }

        private static void AddOptions(ICollection<CompatibilityVersionOption> options,
                                       IEnumerable<GameVersion>               versions,
                                       IReadOnlyCollection<GameVersion>       compatibleVersions,
                                       GameVersion?                           currentVersion,
                                       ISet<GameVersion>                      seen)
        {
            foreach (var version in versions.OrderByDescending(v => v))
            {
                if ((currentVersion != null && version.Equals(currentVersion))
                    || !seen.Add(version))
                {
                    continue;
                }

                options.Add(new CompatibilityVersionOption(version,
                                                           compatibleVersions.Contains(version)));
            }
        }
    }
}
