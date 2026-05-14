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
