using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.Loader;

using log4net;

using CKAN.GUI;

namespace CKAN.LinuxGUI
{
    public sealed class LinuxGuiPluginController : IDisposable
    {
        public LinuxGuiPluginController(GameInstance instance,
                                        bool         activate = true)
        {
            pluginsPath = Path.Combine(instance.CkanDir, "Plugins");
            Directory.CreateDirectory(pluginsPath);

            foreach (var dll in Directory.GetFiles(pluginsPath, "*.dll")
                                         .OrderBy(path => path, Platform.PathComparer))
            {
                LoadAssembly(dll, activate);
            }
        }

        public IReadOnlyList<PluginLoadRecord> ActivePlugins
            => activePlugins.Select(plugin => ToRecord(plugin, true))
                            .OrderBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
                            .ToList();

        public IReadOnlyList<PluginLoadRecord> DormantPlugins
            => dormantPlugins.Select(plugin => ToRecord(plugin, false))
                             .OrderBy(record => record.DisplayName, StringComparer.OrdinalIgnoreCase)
                             .ToList();

        public string PluginsPath => pluginsPath;

        public IReadOnlyList<string> LoadFailures => loadFailures;

        public void AddNewAssemblyToPluginsPath(string sourcePath)
        {
            if (!string.Equals(Path.GetExtension(sourcePath), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only .dll plugin assemblies can be imported.");
            }

            var targetPath = Path.Combine(pluginsPath, Path.GetFileName(sourcePath));
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Copy(sourcePath, targetPath);
            LoadAssembly(targetPath, activate: false);
        }

        public void ActivatePlugin(PluginLoadRecord record)
            => ActivatePlugin(record.Plugin);

        public void DeactivatePlugin(PluginLoadRecord record)
            => DeactivatePlugin(record.Plugin);

        public void ReloadPlugin(PluginLoadRecord record)
        {
            DeactivatePlugin(record.Plugin);
            ActivatePlugin(record.Plugin);
        }

        public void UnloadPlugin(PluginLoadRecord record)
            => UnloadPlugin(record.Plugin);

        public void Dispose()
        {
            foreach (var plugin in activePlugins.ToList())
            {
                TryDeactivate(plugin);
            }
            activePlugins.Clear();
            dormantPlugins.Clear();
            pluginPaths.Clear();
        }

        private void LoadAssembly(string dllPath,
                                  bool   activate)
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
                log.InfoFormat("Loaded plugin assembly \"{0}\"", dllPath);

                var type = TryResolvePluginType(assembly, dllPath);
                if (type == null)
                {
                    loadFailures.Add($"No loadable plugin type was found in {Path.GetFileName(dllPath)}.");
                    return;
                }

                if (Activator.CreateInstance(type) is not IGUIPlugin pluginInstance)
                {
                    loadFailures.Add($"Could not instantiate plugin type {type.FullName} from {Path.GetFileName(dllPath)}.");
                    return;
                }

                if (!ShouldAcceptPlugin(pluginInstance, out var replaced))
                {
                    return;
                }

                if (replaced != null)
                {
                    RemovePlugin(replaced);
                }

                dormantPlugins.Add(pluginInstance);
                pluginPaths[pluginInstance] = dllPath;
                if (activate)
                {
                    ActivatePlugin(pluginInstance);
                }
            }
            catch (Exception ex)
            {
                loadFailures.Add($"Failed to load {Path.GetFileName(dllPath)}: {ex.Message}");
                log.WarnFormat("Failed to load plugin assembly \"{0}\" - {1}",
                               dllPath, ex.Message);
            }
        }

        private Type? TryResolvePluginType(Assembly assembly,
                                           string   dllPath)
        {
            var conventionalName = string.Format("{0}.{0}",
                                                 Path.GetFileNameWithoutExtension(dllPath));
            if (assembly.GetType(conventionalName) is Type conventional
                && typeof(IGUIPlugin).IsAssignableFrom(conventional)
                && !conventional.IsAbstract)
            {
                return conventional;
            }

            try
            {
                return assembly.GetTypes()
                               .FirstOrDefault(type => typeof(IGUIPlugin).IsAssignableFrom(type)
                                                       && !type.IsAbstract
                                                       && type.GetConstructor(Type.EmptyTypes) != null);
            }
            catch (ReflectionTypeLoadException ex)
            {
                var detail = string.Join(" | ",
                                         ex.LoaderExceptions
                                           .Where(err => err != null)
                                           .Select(err => err!.Message));
                loadFailures.Add($"Failed to inspect {Path.GetFileName(dllPath)}: {detail}");
                return null;
            }
        }

        private bool ShouldAcceptPlugin(IGUIPlugin   candidate,
                                        out IGUIPlugin? replaced)
        {
            replaced = null;

            foreach (var existing in activePlugins.Concat(dormantPlugins))
            {
                if (!string.Equals(existing.GetName(), candidate.GetName(), StringComparison.Ordinal))
                {
                    continue;
                }

                if (existing.GetVersion().IsLessThan(candidate.GetVersion()))
                {
                    replaced = existing;
                    return true;
                }

                return false;
            }

            return true;
        }

        private void ActivatePlugin(IGUIPlugin plugin)
        {
            if (!dormantPlugins.Contains(plugin))
            {
                return;
            }

            try
            {
                plugin.Initialize();
                activePlugins.Add(plugin);
                dormantPlugins.Remove(plugin);
                log.InfoFormat("Activated plugin \"{0} - {1}\"", plugin.GetName(), plugin.GetVersion());
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to activate plugin \"{0} - {1}\" - {2}",
                                plugin.GetName(),
                                plugin.GetVersion(),
                                ex);
            }
        }

        private void DeactivatePlugin(IGUIPlugin plugin)
        {
            if (!activePlugins.Contains(plugin))
            {
                return;
            }

            if (TryDeactivate(plugin))
            {
                dormantPlugins.Add(plugin);
                activePlugins.Remove(plugin);
                log.InfoFormat("Deactivated plugin \"{0} - {1}\"", plugin.GetName(), plugin.GetVersion());
            }
        }

        private void UnloadPlugin(IGUIPlugin plugin)
        {
            if (activePlugins.Contains(plugin))
            {
                DeactivatePlugin(plugin);
            }

            dormantPlugins.Remove(plugin);
            pluginPaths.Remove(plugin);
        }

        private bool TryDeactivate(IGUIPlugin plugin)
        {
            try
            {
                plugin.Deinitialize();
                return true;
            }
            catch (Exception ex)
            {
                log.ErrorFormat("Failed to deactivate plugin \"{0} - {1}\" - {2}",
                                plugin.GetName(),
                                plugin.GetVersion(),
                                ex.Message);
                return false;
            }
        }

        private void RemovePlugin(IGUIPlugin plugin)
        {
            if (activePlugins.Contains(plugin))
            {
                TryDeactivate(plugin);
                activePlugins.Remove(plugin);
            }

            dormantPlugins.Remove(plugin);
            pluginPaths.Remove(plugin);
        }

        private PluginLoadRecord ToRecord(IGUIPlugin plugin,
                                          bool       isActive)
            => new PluginLoadRecord(plugin,
                                    pluginPaths.GetValueOrDefault(plugin) ?? "",
                                    isActive);

        private readonly string pluginsPath;
        private readonly HashSet<IGUIPlugin> activePlugins = new HashSet<IGUIPlugin>();
        private readonly HashSet<IGUIPlugin> dormantPlugins = new HashSet<IGUIPlugin>();
        private readonly Dictionary<IGUIPlugin, string> pluginPaths = new Dictionary<IGUIPlugin, string>();
        private readonly List<string> loadFailures = new List<string>();

        private static readonly ILog log = LogManager.GetLogger(typeof(LinuxGuiPluginController));
    }

    public sealed class PluginLoadRecord
    {
        public PluginLoadRecord(IGUIPlugin plugin,
                                string     filePath,
                                bool       isActive)
        {
            Plugin = plugin;
            FilePath = filePath;
            IsActive = isActive;
        }

        public IGUIPlugin Plugin { get; }

        public string FilePath { get; }

        public bool IsActive { get; }

        public string DisplayName => Plugin.ToString();

        public string FileName => string.IsNullOrWhiteSpace(FilePath)
            ? "<unknown>"
            : Path.GetFileName(FilePath);
    }
}
