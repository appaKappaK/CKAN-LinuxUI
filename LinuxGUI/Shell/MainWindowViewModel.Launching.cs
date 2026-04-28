using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;

using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Configuration;
using CKAN.Exporters;
using CKAN.IO;
using CKAN.Types;
using CKAN.Versioning;

namespace CKAN.LinuxGUI
{
    public sealed partial class MainWindowViewModel : ReactiveObject
    {
        private Task RestartToApplyUiScaleAsync()
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                Diagnostics = "The current executable path could not be determined for restart.";
                StatusMessage = "Restart is unavailable right now.";
                return Task.CompletedTask;
            }

            try
            {
                var startInfo = new ProcessStartInfo(processPath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Environment.CurrentDirectory,
                };

                foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
                {
                    startInfo.ArgumentList.Add(arg);
                }

                _ = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("The restart process did not launch.");

                StatusMessage = $"Restarting CKAN Linux to apply {PendingUiScaleLabel} display scale…";
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Restart failed.";
            }

            return Task.CompletedTask;
        }

        private void OpenCurrentGameDirectory()
        {
            if (CurrentInstance == null)
            {
                return;
            }

            LaunchExternal(CurrentInstance.GameDir,
                           $"Opened {CurrentInstance.Name} in your file manager.",
                           "Could not open the current game directory.");
        }

        private async Task PlayGameAsync(GameLaunchMode mode)
        {
            if (CurrentInstance is not GameInstance instance)
            {
                StatusMessage = "Select a game instance before launching.";
                return;
            }

            string? command = FindLaunchCommand(mode);
            if (command == null)
            {
                StatusMessage = mode == GameLaunchMode.Steam
                    ? "No Steam launch command was detected for this instance."
                    : "No direct launch command was detected for this instance.";
                return;
            }

            if (!await ConfirmLaunchAgainstIncompatibleModsAsync(instance))
            {
                StatusMessage = "Launch cancelled.";
                return;
            }

            instance.PlayGame(command,
                              () => Dispatcher.UIThread.Post(() =>
                              {
                                  RefreshInstanceSummaries();
                                  if (mode == GameLaunchMode.Direct)
                                  {
                                      StatusMessage = $"{instance.Name} launch process exited.";
                                  }
                              }));

            StatusMessage = mode == GameLaunchMode.Steam
                ? $"Launching {instance.Name} via Steam..."
                : $"Launching {instance.Name} directly...";
        }

        private async Task<bool> ConfirmLaunchAgainstIncompatibleModsAsync(GameInstance instance)
        {
            if (CurrentRegistry == null)
            {
                return true;
            }

            var suppressedIdentifiers = instance.GetSuppressedCompatWarningIdentifiers;
            var incompatible = CurrentRegistry.IncompatibleInstalled(instance.VersionCriteria())
                                              .Where(module => !module.Module.IsDLC
                                                               && !suppressedIdentifiers.Contains(module.identifier))
                                              .ToList();
            if (incompatible.Count == 0)
            {
                return true;
            }

            if (ConfirmIncompatibleLaunchAsync == null)
            {
                return true;
            }

            string details = string.Join(Environment.NewLine,
                                         incompatible.Select(module =>
                                             $"- {module.Module} ({module.Module.CompatibleGameVersions(instance.Game)})"));
            string prompt = "Some installed modules are incompatible with this game version. "
                            + "It might not be safe to launch the game."
                            + Environment.NewLine
                            + Environment.NewLine
                            + details
                            + Environment.NewLine
                            + Environment.NewLine
                            + "Launch anyway?";
            return await ConfirmIncompatibleLaunchAsync(prompt);
        }

        private string? FindLaunchCommand(GameLaunchMode mode)
        {
            if (CurrentInstance is not GameInstance instance)
            {
                return null;
            }

            try
            {
                return GameCommandLineConfigStore.Load(instance, CurrentSteamLibrary)
                                                 .FirstOrDefault(command =>
                                                     mode == GameLaunchMode.Steam
                                                         ? SteamLibrary.IsSteamCmdLine(command)
                                                         : !SteamLibrary.IsSteamCmdLine(command));
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                return null;
            }
        }

        private void OpenSelectedModCacheLocation()
        {
            if (ResolveSelectedModCachedArchivePath() is not string path)
            {
                StatusMessage = "No cached archive is available for this mod.";
                return;
            }

            Utilities.OpenFileBrowser(path);
            StatusMessage = "Opened cached archive location in your file manager.";
        }

        private void OpenSelectedModResourceLink(ModResourceLinkItem? link)
        {
            if (link == null || string.IsNullOrWhiteSpace(link.Url))
            {
                StatusMessage = "No link is available for this resource.";
                return;
            }

            LaunchExternal(link.Url,
                           $"Opened {link.Label.ToLowerInvariant()}.",
                           $"Could not open {link.Label.ToLowerInvariant()}.");
        }

        internal void OpenSelectedModResourceLinkFromUi(ModResourceLinkItem? link)
            => OpenSelectedModResourceLink(link);

        private void OpenUserGuide()
            => LaunchExternal(HelpURLs.UserGuide,
                              "Opened the CKAN user guide.",
                              "Could not open the CKAN user guide.");

        private void OpenDiscord()
            => LaunchExternal(HelpURLs.CKANDiscord,
                              "Opened the CKAN Discord invite.",
                              "Could not open the CKAN Discord invite.");

        private void OpenGameSupport()
        {
            if (CurrentInstance == null)
            {
                return;
            }

            LaunchExternal(CurrentInstance.Game.ModSupportURL.ToString(),
                           "Opened the KSP mod support page.",
                           "Could not open the KSP mod support page.");
        }

        private void ReportClientIssue()
            => LaunchExternal(HelpURLs.CKANIssues,
                              "Opened the CKAN client issue tracker.",
                              "Could not open the CKAN client issue tracker.");

        private void ReportMetadataIssue()
        {
            if (CurrentInstance == null)
            {
                return;
            }

            LaunchExternal(CurrentInstance.Game.MetadataBugtrackerURL.ToString(),
                           "Opened the mod metadata issue tracker.",
                           "Could not open the mod metadata issue tracker.");
        }

        private void LaunchExternal(string target,
                                    string successMessage,
                                    string failureMessage)
        {
            if (Utilities.ProcessStartURL(target))
            {
                StatusMessage = successMessage;
            }
            else
            {
                Diagnostics = $"Failed to launch: {target}";
                StatusMessage = failureMessage;
            }
        }
    }
}
