using System.Linq;

using NUnit.Framework;

using CKAN.App.Models;
using CKAN.App.Services;

namespace Tests.App.Services
{
    [TestFixture]
    public sealed class ChangesetServiceTests
    {
        [Test]
        public void QueueInstall_WithoutExplicitTarget_LeavesVersionForRegistryResolution()
        {
            var changes = new ChangesetService();

            changes.QueueInstall(new ModListItem
            {
                Identifier    = "Waterfall",
                Name          = "Waterfall Core",
                LatestVersion = "0.10.5",
            });

            var action = changes.CurrentApplyQueue.Single();

            Assert.Multiple(() =>
            {
                Assert.That(action.ActionKind, Is.EqualTo(QueuedActionKind.Install));
                Assert.That(action.TargetVersion, Is.Empty);
                Assert.That(action.DetailText, Is.EqualTo("Install latest available version"));
                Assert.That(action.VersionText, Is.EqualTo("Latest"));
            });
        }

        [Test]
        public void QueueInstall_WithExplicitTarget_UsesSelectedVersion()
        {
            var changes = new ChangesetService();

            changes.QueueInstall(new ModListItem
            {
                Identifier    = "Waterfall",
                Name          = "Waterfall Core",
                LatestVersion = "0.10.5",
            }, "0.11.0");

            var action = changes.CurrentApplyQueue.Single();

            Assert.Multiple(() =>
            {
                Assert.That(action.ActionKind, Is.EqualTo(QueuedActionKind.Install));
                Assert.That(action.TargetVersion, Is.EqualTo("0.11.0"));
                Assert.That(action.DetailText, Is.EqualTo("Install 0.11.0"));
                Assert.That(action.VersionText, Is.EqualTo("0.11.0"));
                Assert.That(action.DownloadText, Is.EqualTo("Download"));
            });
        }

        [Test]
        public void QueueUpdate_WithExplicitOlderTarget_ShowsTheSelectedVersionAndDownloadAction()
        {
            var changes = new ChangesetService();

            changes.QueueUpdate(new ModListItem
            {
                Identifier       = "Kopernicus",
                Name             = "Kopernicus",
                InstalledVersion = "2:release-1.12.1-249",
                LatestVersion    = "2:release-1.12.1-249",
                HasVersionUpdate = false,
            }, "2:release-1.12.1-247");

            var action = changes.CurrentApplyQueue.Single();

            Assert.Multiple(() =>
            {
                Assert.That(action.ActionText, Is.EqualTo("Downgrade"));
                Assert.That(action.VersionText, Is.EqualTo("2:release-1.12.1-247"));
                Assert.That(action.DownloadText, Is.EqualTo("Download"));
            });
        }

        [Test]
        public void PreserveAutoRemoval_AddsIdentifierUntilApplyQueueIsCleared()
        {
            var changes = new ChangesetService();

            changes.PreserveAutoRemoval("ParallaxContinued");

            Assert.That(changes.PreservedAutoRemovalIdentifiers,
                        Does.Contain("ParallaxContinued"));

            changes.ClearApplyQueue();

            Assert.That(changes.PreservedAutoRemovalIdentifiers, Is.Empty);
        }

        [Test]
        public void QueueUpdate_WithoutExplicitTarget_LeavesVersionForRegistryResolution()
        {
            var changes = new ChangesetService();

            changes.QueueUpdate(new ModListItem
            {
                Identifier       = "Waterfall",
                Name             = "Waterfall Core",
                InstalledVersion = "0.10.5",
                LatestVersion    = "0.10.5",
                HasVersionUpdate = true,
            });

            var action = changes.CurrentApplyQueue.Single();

            Assert.Multiple(() =>
            {
                Assert.That(action.ActionKind, Is.EqualTo(QueuedActionKind.Update));
                Assert.That(action.TargetVersion, Is.Empty);
                Assert.That(action.DetailText, Is.EqualTo("0.10.5 -> latest compatible version"));
                Assert.That(action.VersionText, Is.EqualTo("Latest"));
            });
        }

        [Test]
        public void QueueUpdate_WithExplicitTarget_UsesSelectedVersion()
        {
            var changes = new ChangesetService();

            changes.QueueUpdate(new ModListItem
            {
                Identifier       = "Waterfall",
                Name             = "Waterfall Core",
                InstalledVersion = "0.10.5",
                LatestVersion    = "0.10.5",
                HasVersionUpdate = true,
            }, "0.11.0");

            var action = changes.CurrentApplyQueue.Single();

            Assert.Multiple(() =>
            {
                Assert.That(action.ActionKind, Is.EqualTo(QueuedActionKind.Update));
                Assert.That(action.TargetVersion, Is.EqualTo("0.11.0"));
                Assert.That(action.DetailText, Is.EqualTo("0.10.5 -> 0.11.0"));
            });
        }
    }
}
