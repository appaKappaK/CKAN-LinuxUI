using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Types;
using CKAN.Versioning;

namespace CKAN.LinuxGUI
{
    public partial class MainWindow : Window
    {
        private void RefreshPluginControllerForCurrentInstance(GameInstance? instance)
        {
            if (instance == null)
            {
                DisposePluginController();
                return;
            }

            var instanceDir = instance.GameDir;
            if (string.IsNullOrWhiteSpace(instanceDir))
            {
                DisposePluginController();
                return;
            }

            if (pluginController != null
                && string.Equals(pluginControllerInstanceDir, instanceDir, StringComparison.Ordinal))
            {
                return;
            }

            DisposePluginController();
            pluginController = new LinuxGuiPluginController(instance);
            pluginControllerInstanceDir = instanceDir;
        }

        private void DisposePluginController()
        {
            pluginController?.Dispose();
            pluginController = null;
            pluginControllerInstanceDir = null;
        }
    }
}
