using System.Collections.Concurrent;

namespace CKAN
{
    /// <summary>
    /// Queries upstream CKAN release metadata for the bundled core version display.
    /// CKAN Linux updates are handled separately by LinuxGuiUpdateService.
    /// </summary>
    public class AutoUpdate
    {
        public AutoUpdate(string? userAgent)
        {
            this.userAgent = userAgent;
        }

        public CkanUpdate GetUpdate(bool devBuild, bool bypassCache = false)
            => bypassCache ? updates.AddOrUpdate(devBuild, GetUpdateUncached,
                                                           (dev, upd) => GetUpdateUncached(dev))
                           : updates.GetOrAdd(devBuild, GetUpdateUncached);

        private CkanUpdate GetUpdateUncached(bool devBuild)
            => devBuild ? new S3BuildCkanUpdate(null, userAgent)
                        : new GitHubReleaseCkanUpdate(null, userAgent);

        private readonly ConcurrentDictionary<bool, CkanUpdate> updates = new ConcurrentDictionary<bool, CkanUpdate>();
        private readonly string? userAgent;
    }
}
