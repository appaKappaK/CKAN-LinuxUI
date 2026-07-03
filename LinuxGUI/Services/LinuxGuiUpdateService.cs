using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

using CKAN.Configuration;
using CKAN.Versioning;

namespace CKAN.LinuxGUI.Services
{
    internal static class LinuxGuiUpdateService
    {
        public const string ReleasesUrl = "https://github.com/appaKappaK/CKAN-LinuxUI/releases";

        public const bool SupportsReleaseChecks = false;

        private static readonly Uri ReleasesApiUrl =
            new Uri("https://api.github.com/repos/appaKappaK/CKAN-LinuxUI/releases");

        public static string CurrentVersion
            => FormatAssemblyVersion(Assembly.GetExecutingAssembly());

        public static LinuxGuiUpdateInfo GetLatest(bool includePrereleases,
                                                   IConfiguration? configuration,
                                                   string? userAgent = null)
        {
            var token = configuration?.TryGetAuthToken(ReleasesApiUrl.Host, out string? storedToken) == true
                ? storedToken
                : Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            var content = Net.DownloadText(ReleasesApiUrl,
                                           userAgent,
                                           token);
            var releases = content == null
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<List<GitHubRelease>>(content);
            var release = releases?
                          .Where(item => !item.Draft)
                          .FirstOrDefault(item => includePrereleases || !item.Prerelease);
            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            {
                throw new Kraken("Could not fetch the latest CKAN Linux release.");
            }

            return new LinuxGuiUpdateInfo(release.TagName,
                                          string.IsNullOrWhiteSpace(release.Name)
                                              ? release.TagName
                                              : release.Name!,
                                          release.Prerelease,
                                          release.HtmlUrl ?? ReleasesUrl);
        }

        public static bool IsNewerThanCurrent(string version)
        {
            try
            {
                return new ModuleVersion(version).CompareTo(new ModuleVersion(CurrentVersion)) > 0;
            }
            catch
            {
                return !string.Equals(version,
                                      CurrentVersion,
                                      StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string FormatAssemblyVersion(Assembly assembly)
        {
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                  ?.InformationalVersion
                       ?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()
                                  ?.Version
                       ?? assembly.GetName().Version?.ToString()
                       ?? "unknown";
            return version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? version
                : $"v{version}";
        }

        private sealed class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }

            [JsonPropertyName("draft")]
            public bool Draft { get; set; }

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; set; }
        }
    }

    internal sealed class LinuxGuiUpdateInfo
    {
        public LinuxGuiUpdateInfo(string version,
                                  string name,
                                  bool   prerelease,
                                  string releaseUrl)
        {
            Version = version;
            Name = name;
            Prerelease = prerelease;
            ReleaseUrl = releaseUrl;
        }

        public string Version { get; }

        public string Name { get; }

        public bool Prerelease { get; }

        public string ReleaseUrl { get; }

        public string DisplayVersion
            => string.Equals(Version, Name, StringComparison.OrdinalIgnoreCase)
                ? Version
                : $"{Version} ({Name})";
    }
}
