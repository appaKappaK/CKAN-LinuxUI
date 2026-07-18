using System;
using System.IO;
using System.Net;

using NUnit.Framework;
using Newtonsoft.Json;

using CKAN;
using Tests.Data;

namespace Tests.Core.Net.AutoUpdateTests
{
    [TestFixture]
    public class AutoUpdateTests
    {
        [TestCase(true),
         TestCase(false)]
        // This could fail if run during a release, so it's marked as Flaky.
        [Category("FlakyNetwork"),
         Category("Online")]
        public void GetUpdate_DevBuildOrStable_Works(bool devBuild)
        {
            // Force-allow TLS 1.2 for HTTPS URLs, because GitHub requires it.
            // This is on by default in .NET 4.6, but not in 4.5.
            #pragma warning disable SYSLIB0014
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12
                                                  | SecurityProtocolType.Tls13;
            #pragma warning restore SYSLIB0014

            var updater = new AutoUpdate(null);
            var update  = updater.GetUpdate(devBuild);

            // Is is a *really* basic test to just make sure we get release info
            // if we ask for it.
            Assert.IsNotNull(update.ReleaseNotes);
            Assert.IsNotNull(update.Version);
        }

        [Test]
        [TestCase("aaa\r\n---\r\nbbb", "bbb", "Release note marker included")]
        [TestCase("aaa\r\nbbb", "aaa\r\nbbb", "No release note marker")]
        [TestCase("aaa\r\n---\r\nbbb\r\n---\r\nccc", "bbb\r\n---\r\nccc", "Multi release notes markers")]
        public void ExtractReleaseNotes(string body, string expected, string comment)
        {
            Assert.AreEqual(expected,
                            GitHubReleaseCkanUpdate.ExtractReleaseNotes(body),
                            comment);
        }

        [Test]
        public void GitHubReleaseCkanUpdate_NormalUpdate_ParsedCorrectly()
        {
            // Arrange
            const string releaseJSON = @"{
                ""name"": ""Wallops"",
                ""tag_name"": ""v1.25.0"",
                ""body"": ""[![](https://upload.wikimedia.org/wikipedia/commons/thumb/2/24/NASA_Wallops_Flight_Facility%2C_2010.jpg/780px-NASA_Wallops_Flight_Facility%2C_2010.jpg)](https://en.wikipedia.org/wiki/Wallops_Flight_Facility)\r\n\r\n---\r\nGreatest release notes of all time""
            }";

            // Act
            var relInfo = JsonConvert.DeserializeObject<GitHubReleaseInfo>(releaseJSON);
            var upd     = new GitHubReleaseCkanUpdate(relInfo);

            // Assert
            Assert.AreEqual("v1.25.0", relInfo?.tag_name);
            Assert.AreEqual("Wallops", relInfo?.name);
            Assert.AreEqual("v1.25.0 aka Wallops", upd.Version?.ToString());
            Assert.AreEqual("Greatest release notes of all time", upd.ReleaseNotes);
        }

        [Test]
        public void S3BuildCkanUpdate_Constructor_ParsedCorrectly()
        {
            // Arrange / Act
            var upd = new S3BuildCkanUpdate(
                JsonConvert.DeserializeObject<S3BuildVersionInfo>(
                    File.ReadAllText(TestData.DataFile("version.json"))));

            // Assert
            Assert.AreEqual("v1.34.5.24015 aka dev",
                            upd.Version?.ToString());
            Assert.AreEqual("### Internal\n\n- [Policy] Fix #3518 rewrite de-indexing policy (#3993 by: JonnyOThan; reviewed: HebaruSan)",
                            upd.ReleaseNotes);
        }

    }
}
