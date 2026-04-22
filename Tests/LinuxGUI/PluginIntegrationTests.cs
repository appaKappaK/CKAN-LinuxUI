#if NET8_0
using System.IO;
using System.Linq;

using NUnit.Framework;

using CKAN;
using CKAN.GUI;
using CKAN.LinuxGUI;
using CKAN.Games.KerbalSpaceProgram;

using Tests.Core.Configuration;
using Tests.Data;

namespace Tests.LinuxGUI
{
    [TestFixture]
    public sealed class PluginIntegrationTests
    {
        [Test]
        public void LinuxGuiPluginController_ActivatesLegacyPlugin_AndWritesMarker()
        {
            using var ksp = new DisposableKSP("plugin-smoke", new KerbalSpaceProgram());
            using var config = new FakeConfiguration(ksp.KSP, ksp.KSP.Name);
            using var manager = new GameInstanceManager(new NullUser(), config);
            manager.SetCurrentInstance(ksp.KSP);
            Main.SetInstance(manager, new NullUser());

            var pluginsDir = Path.Combine(ksp.KSP.CkanDir, "Plugins");
            Directory.CreateDirectory(pluginsDir);

            var pluginDll = BuiltSamplePluginPath();
            File.Copy(pluginDll,
                      Path.Combine(pluginsDir, Path.GetFileName(pluginDll)),
                      overwrite: true);

            using var controller = new LinuxGuiPluginController(ksp.KSP);

            var markerPath = Path.Combine(ksp.KSP.CkanDir, "plugin-smoke-marker.txt");
            Assert.That(controller.LoadFailures, Is.Empty);
            Assert.That(controller.ActivePlugins.Count, Is.EqualTo(1));
            Assert.That(controller.ActivePlugins.Single().DisplayName,
                        Does.Contain("PluginSmokePlugin"));
            Assert.That(File.Exists(markerPath), Is.True);
            Assert.That(File.ReadAllText(markerPath), Does.Contain(ksp.KSP.Name));
            Assert.That(File.ReadAllText(markerPath), Does.Contain("manager"));
            Assert.That(File.ReadAllText(markerPath), Does.Contain("user"));

            Main.ClearInstance();
        }

        private static string BuiltSamplePluginPath()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(TestData.DataDir, "..", ".."));
            var candidates = new[]
            {
                Path.Combine(repoRoot, "_build", "out", "PluginSmokePlugin", "Debug", "bin", "net8.0", "PluginSmokePlugin.dll"),
                Path.Combine(repoRoot, "_build", "out", "PluginSmokePlugin", "VSCodeIDE", "bin", "net8.0", "PluginSmokePlugin.dll"),
            };
            var path = candidates.FirstOrDefault(File.Exists);
            Assert.That(path, Is.Not.Null,
                        $"Sample plugin not found. Checked: {string.Join(", ", candidates)}");
            return path!;
        }
    }
}
#endif
