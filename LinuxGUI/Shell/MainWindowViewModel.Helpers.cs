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
        private void PruneQueuedAutodetectedRemovals(IReadOnlyList<ModListItem>? catalogItems = null)
        {
            var autodetectedIdentifiers = (catalogItems ?? Mods)
                                          .Where(mod => mod.IsAutodetected)
                                          .Select(mod => mod.Identifier)
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (autodetectedIdentifiers.Count == 0)
            {
                return;
            }

            var invalidRemovals = changesetService.CurrentApplyQueue
                                                  .Where(action => action.ActionKind == QueuedActionKind.Remove
                                                                   && autodetectedIdentifiers.Contains(action.Identifier))
                                                  .ToList();
            if (invalidRemovals.Count == 0)
            {
                return;
            }

            foreach (var action in invalidRemovals)
            {
                changesetService.Remove(action.Identifier);
            }

            StatusMessage = invalidRemovals.Count == 1
                ? $"Removed queued removal for {invalidRemovals[0].Name}. It was detected outside CKAN and must be removed manually from GameData."
                : $"Removed {invalidRemovals.Count} queued removals for mods detected outside CKAN. They must be removed manually from GameData.";
        }

        private void PruneQueuedAutodetectedDownloads(IReadOnlyList<ModListItem>? catalogItems = null)
        {
            var autodetectedIdentifiers = (catalogItems ?? Mods)
                                          .Where(mod => mod.IsAutodetected)
                                          .Select(mod => mod.Identifier)
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (autodetectedIdentifiers.Count == 0)
            {
                return;
            }

            var invalidDownloads = changesetService.CurrentDownloadQueue
                                                   .Where(action => autodetectedIdentifiers.Contains(action.Identifier))
                                                   .ToList();
            if (invalidDownloads.Count == 0)
            {
                return;
            }

            foreach (var action in invalidDownloads)
            {
                changesetService.Remove(action.Identifier);
            }

            StatusMessage = invalidDownloads.Count == 1
                ? $"Removed queued cache action for {invalidDownloads[0].Name}. It is managed outside CKAN and cannot be added to the cache."
                : $"Removed {invalidDownloads.Count} queued cache actions for mods managed outside CKAN. External mods cannot be added to the cache.";
        }

        private bool IsCurrentSelectedModRequest(string identifier,
                                                 int    requestId)
            => requestId == selectedModLoadRequestId
               && string.Equals(identifier, SelectedMod?.Identifier, StringComparison.OrdinalIgnoreCase);

        private bool MessageContains(string value)
            => StatusMessage?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool CanQueueInstall(ModListItem mod)
            => !mod.IsInstalled && !mod.IsIncompatible;

        private static bool CanQueueUpdate(ModListItem mod)
            => mod.IsInstalled && mod.HasVersionUpdate;

        private static bool CanQueueRemove(ModListItem mod)
            => mod.IsInstalled && !mod.IsAutodetected && !mod.HasVersionUpdate;

        private static string CountLabel(int count,
                                         string singular,
                                         string plural)
            => count == 1
                ? $"1 {singular}"
                : $"{count} {plural}";

        private static string Pluralize(int count)
            => count == 1 ? "" : "s";
    }
}
