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
        private void PublishInstanceStateLabels()
        {
            UpdateCurrentInstanceContext();
            UpdateReadyInstanceHint();
            this.RaisePropertyChanged(nameof(HasInstances));
            this.RaisePropertyChanged(nameof(HasCurrentInstance));
            this.RaisePropertyChanged(nameof(CurrentInstance));
            this.RaisePropertyChanged(nameof(CurrentRegistry));
            this.RaisePropertyChanged(nameof(CurrentCache));
            PublishLaunchCommandState();
            this.RaisePropertyChanged(nameof(InstanceCountLabel));
            PublishCompatibleGameVersionState();
            this.RaisePropertyChanged(nameof(ShowHeaderInstanceSwitcher));
            this.RaisePropertyChanged(nameof(ShowPassiveHeaderInstanceLabel));
            this.RaisePropertyChanged(nameof(ShowStartupInstancePanel));
            this.RaisePropertyChanged(nameof(ShowReadyInstancePanel));
            this.RaisePropertyChanged(nameof(SelectedInstanceIsCurrent));
            this.RaisePropertyChanged(nameof(ShowSwitchSelectedInstanceAction));
            this.RaisePropertyChanged(nameof(SelectedInstanceContextTitle));
            UpdateSelectedModCachedArchivePath();
        }

        private void PublishLaunchCommandState()
        {
            this.RaisePropertyChanged(nameof(CanPlayDirect));
            this.RaisePropertyChanged(nameof(CanPlayViaSteam));
        }

        private void ClearCatalogState()
        {
            allCatalogItems = Array.Empty<ModListItem>();
            Mods.Clear();
            AvailableTagOptions.Clear();
            AvailableLabelOptions.Clear();
            ShowTagFilterPicker = false;
            ShowLabelFilterPicker = false;
            filterOptionCounts = new FilterOptionCounts();
            hasFilterOptionCounts = false;
            ResetSelectedModDetails();
            SelectedMod = null;
            CatalogStatusMessage = "Select an active instance to view its mod catalog.";
            PublishCatalogStateLabels();
            PublishFilterOptionCountLabels();
            this.RaisePropertyChanged(nameof(HasAvailableTagOptions));
            this.RaisePropertyChanged(nameof(TagFilterPickerSummary));
            this.RaisePropertyChanged(nameof(SelectedCategoryCount));
            this.RaisePropertyChanged(nameof(HasSelectedTagFilter));
            this.RaisePropertyChanged(nameof(HasAvailableLabelOptions));
            this.RaisePropertyChanged(nameof(LabelFilterPickerSummary));
            this.RaisePropertyChanged(nameof(HasSelectedLabelFilter));
        }
    }
}
