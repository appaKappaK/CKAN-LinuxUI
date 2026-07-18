using System;

using Autofac;
using Newtonsoft.Json;

using CKAN.Configuration;
using CKAN.Versioning;

namespace CKAN
{
    /// <summary>
    /// Represents a CKAN release on GitHub
    /// </summary>
    public class GitHubReleaseCkanUpdate : CkanUpdate
    {
        /// <summary>
        /// Initialize the Object
        /// </summary>
        /// <param name="releaseJson">JSON representation of release</param>
        /// <param name="userAgent">User agent to use for the request</param>
        public GitHubReleaseCkanUpdate(GitHubReleaseInfo? releaseJson = null, string? userAgent = null)
        {
            if (releaseJson == null)
            {
                var coreConfig = ServiceLocator.Container.Resolve<IConfiguration>();
                var token = coreConfig.TryGetAuthToken(latestCKANReleaseApiUrl.Host, out string? t)
                                ? t
                                : Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                releaseJson = Net.DownloadText(latestCKANReleaseApiUrl, userAgent, token) is string content
                              ? JsonConvert.DeserializeObject<GitHubReleaseInfo>(content)
                              : null;
            }
            if (releaseJson is null
                || releaseJson.tag_name is null
                || releaseJson.name is null
                || releaseJson.body is null)
            {
                throw new Kraken(Properties.Resources.AutoUpdateNotFetched);
            }

            Version = new CkanModuleVersion(releaseJson.tag_name.ToString(),
                                            releaseJson.name.ToString());
            ReleaseNotes = ExtractReleaseNotes(releaseJson.body.ToString());

        }

        /// <summary>
        /// Extracts release notes from the body of text provided by the github API.
        /// By default this is everything after the first three dashes on a line by
        /// itself, but as a fallback we'll use the whole body if not found.
        /// </summary>
        /// <returns>The release notes.</returns>
        internal static string ExtractReleaseNotes(string releaseBody)
        {
            const string divider = "\r\n---\r\n";
            // Get at most two pieces, the first is the image, the second is the release notes
            //return releaseBody.Split(new string[] { divider },
            //                         2, StringSplitOptions.None) switch {
            //    [_, string val, ..] => val,
            //    [string val]        => val,
            //    _                   => "",
            //};
            var array = releaseBody.Split(new string[] { divider },
                                          2, StringSplitOptions.None);
            return array.Length > 1
                   && array[1] is string val
                       ? val
                       : array.Length == 1
                         && array[0] is string val2
                             ? val2
                             : "";
        }

        private static readonly Uri latestCKANReleaseApiUrl =
            new Uri("https://api.github.com/repos/KSP-CKAN/CKAN/releases/latest");
    }
}
