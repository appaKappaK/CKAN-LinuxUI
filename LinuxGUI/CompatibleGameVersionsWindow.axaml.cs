using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Avalonia.Controls;
using ReactiveUI;

using CKAN.Versioning;

namespace CKAN.LinuxGUI
{
    public partial class CompatibleGameVersionsWindow : Window
    {
        private readonly EditorViewModel? viewModel;

        public CompatibleGameVersionsWindow()
        {
            InitializeComponent();
        }

        public CompatibleGameVersionsWindow(GameInstance instance)
        {
            InitializeComponent();
            viewModel = new EditorViewModel(instance);
            DataContext = viewModel;
        }

        public IReadOnlyCollection<GameVersion> SelectedVersions
            => viewModel == null
                ? Array.Empty<GameVersion>()
                : viewModel.Options.Where(option => option.IsSelected)
                                   .Select(option => option.Version)
                                   .ToList();

        private void ClearSelectionButton_OnClick(object? sender,
                                                  Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel?.ClearSelection();

        private void AddVersionButton_OnClick(object? sender,
                                              Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel?.TryAddVersion();

        private void CancelButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);

        private void AcceptButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
            => Close(true);

        private sealed class EditorViewModel : ReactiveObject
        {
            private string addVersionText = "";
            private string addVersionValidationMessage = "";

            public EditorViewModel(GameInstance instance)
            {
                CurrentGameVersion = instance.Version();
                InstallationPath = Platform.FormatPath(instance.GameDir);
                CurrentGameVersionLabel = instance.CompatibleVersionsAreFromDifferentGameVersion
                                          && instance.GameVersionWhenCompatibleVersionsWereStored != null
                    ? $"{instance.Version()} (previous game version: {instance.GameVersionWhenCompatibleVersionsWereStored})"
                    : instance.Version()?.ToString() ?? "<NONE>";
                CompatibilityHint = "Note: Adding a version like \"1.12\" will treat all mods compatible with 1.12.x as compatible with this game instance. If the game is updated, review these settings again.";
                SafetyWarningText = "Warning: There is no way to verify that a mod is truly compatible with versions selected here. Please act carefully.";
                ShowStoredCompatibilityWarning = instance.CompatibleVersionsAreFromDifferentGameVersion;
                StoredCompatibilityWarningText = instance.GameVersionWhenCompatibleVersionsWereStored == null
                    ? "Default compatibility was inferred automatically from your installed game version. Review these selections before trusting older mods."
                    : "The game has been updated since you last reviewed these compatible game versions. Please make sure these settings are still correct.";
                Options = new ObservableCollection<CompatibilityVersionOption>(
                    CompatibilityVersionOptionBuilder.Build(instance));
            }

            public ObservableCollection<CompatibilityVersionOption> Options { get; }

            public GameVersion? CurrentGameVersion { get; }

            public string InstallationPath { get; }

            public string CurrentGameVersionLabel { get; }

            public string CompatibilityHint { get; }

            public string SafetyWarningText { get; }

            public bool ShowStoredCompatibilityWarning { get; }

            public string StoredCompatibilityWarningText { get; }

            public string AddVersionText
            {
                get => addVersionText;
                set => this.RaiseAndSetIfChanged(ref addVersionText, value);
            }

            public string AddVersionValidationMessage
            {
                get => addVersionValidationMessage;
                private set
                {
                    this.RaiseAndSetIfChanged(ref addVersionValidationMessage, value);
                    this.RaisePropertyChanged(nameof(ShowAddVersionValidationMessage));
                }
            }

            public bool ShowAddVersionValidationMessage
                => !string.IsNullOrWhiteSpace(AddVersionValidationMessage);

            public void ClearSelection()
            {
                foreach (var option in Options)
                {
                    option.IsSelected = false;
                }
                AddVersionValidationMessage = "";
            }

            public void TryAddVersion()
            {
                var text = AddVersionText.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                if (text.Equals("any", StringComparison.OrdinalIgnoreCase)
                    || !GameVersion.TryParse(text, out var version)
                    || version == null)
                {
                    AddVersionValidationMessage = "Version has invalid format.";
                    return;
                }

                if (CurrentGameVersion != null && version.Equals(CurrentGameVersion))
                {
                    AddVersionValidationMessage = "The installed game version is always included automatically.";
                    return;
                }

                var existing = Options.FirstOrDefault(option => option.Version.Equals(version));
                if (existing != null)
                {
                    existing.IsSelected = true;
                    AddVersionText = "";
                    AddVersionValidationMessage = "";
                    return;
                }

                int insertIndex = 0;
                while (insertIndex < Options.Count
                       && Options[insertIndex].Version.CompareTo(version) > 0)
                {
                    insertIndex++;
                }

                Options.Insert(insertIndex, new CompatibilityVersionOption(version, isSelected: true));
                AddVersionText = "";
                AddVersionValidationMessage = "";
            }
        }

    }
}
