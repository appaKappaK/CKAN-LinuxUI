using System.IO;

using CKAN.GUI;
using CKAN.Versioning;

namespace PluginSmokePlugin
{
    public sealed class PluginSmokePlugin : IGUIPlugin
    {
        public override string GetName()
            => "PluginSmokePlugin";

        public override ModuleVersion GetVersion()
            => new ModuleVersion("1.0.0");

        public override void Initialize()
        {
            var markerDir = Main.Instance?.CurrentInstance?.CkanDir;
            if (string.IsNullOrWhiteSpace(markerDir))
            {
                return;
            }

            var markerPath = Path.Combine(markerDir, "plugin-smoke-marker.txt");
            var payload = string.Join("|",
                                      Main.Instance?.CurrentInstance?.Name ?? "<none>",
                                      Main.Instance?.Manager != null ? "manager" : "no-manager",
                                      Main.Instance?.currentUser != null ? "user" : "no-user");
            File.WriteAllText(markerPath, payload);
        }

        public override void Deinitialize()
        {
            var markerDir = Main.Instance?.CurrentInstance?.CkanDir;
            if (string.IsNullOrWhiteSpace(markerDir))
            {
                return;
            }

            File.WriteAllText(Path.Combine(markerDir, "plugin-smoke-unloaded.txt"),
                              "deinitialized");
        }
    }
}
