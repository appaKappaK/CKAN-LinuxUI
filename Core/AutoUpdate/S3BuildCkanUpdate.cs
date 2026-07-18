using System;

using Newtonsoft.Json;

using CKAN.Versioning;

namespace CKAN
{
    public class S3BuildCkanUpdate : CkanUpdate
    {
        public S3BuildCkanUpdate(S3BuildVersionInfo? versionJson = null, string? userAgent = null)
        {
            versionJson ??= Net.DownloadText(new Uri(S3BaseUrl, VersionJsonUrlPiece), userAgent) is string content
                                ? JsonConvert.DeserializeObject<S3BuildVersionInfo>(content)
                                : null;
            if (versionJson is null || versionJson.version is null)
            {
                throw new Kraken(Properties.Resources.AutoUpdateNotFetched);
            }
            Version      = new CkanModuleVersion(versionJson.version.ToString(), "dev");
            ReleaseNotes = versionJson.changelog;
        }

        private static readonly Uri S3BaseUrl =
            new Uri("https://ksp-ckan.s3-us-west-2.amazonaws.com/");
        private const           string VersionJsonUrlPiece = "version.json";
    }
}
