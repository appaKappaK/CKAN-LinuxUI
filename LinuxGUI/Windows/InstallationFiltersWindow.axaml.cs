using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

using Avalonia.Controls;
using ReactiveUI;

using CKAN.Configuration;

namespace CKAN.LinuxGUI
{
    public partial class InstallationFiltersWindow : Window
    {
        private readonly IConfiguration? configuration;
        private readonly GameInstance? instance;
        private readonly EditorViewModel viewModel;

        public InstallationFiltersWindow()
        {
            InitializeComponent();
            viewModel = new EditorViewModel("Global filters",
                                            "",
                                            "Instance filters",
                                            "",
                                            new Dictionary<string, string[]>());
            DataContext = viewModel;
        }

        public InstallationFiltersWindow(IConfiguration configuration,
                                         GameInstance    instance)
        {
            InitializeComponent();
            this.configuration = configuration;
            this.instance = instance;
            viewModel = new EditorViewModel($"Global Filters for {instance.Game.ShortName}",
                                            string.Join(Environment.NewLine,
                                                        configuration.GetGlobalInstallFilters(instance.Game)),
                                            $"Instance Filters for {instance.Name}",
                                            string.Join(Environment.NewLine,
                                                        instance.InstallFilters),
                                            instance.Game.InstallFilterPresets);
            DataContext = viewModel;
        }

        public bool Changed { get; private set; }

        private void AddPresetButton_OnClick(object? sender,
                                             Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button { Tag: string presetName })
            {
                viewModel.AddPreset(presetName);
            }
        }

        private void HelpButton_OnClick(object? sender,
                                        Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.StatusMessage = Utilities.ProcessStartURL(HelpURLs.Filters)
                ? "Opened installation-filter help."
                : "Could not open installation-filter help.";

        private void CancelButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);

        private void AcceptButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (configuration == null || instance == null)
            {
                Close(false);
                return;
            }

            if (!viewModel.TryApply(configuration, instance, out var changed))
            {
                return;
            }

            Changed = changed;
            Close(true);
        }

        private sealed class EditorViewModel : ReactiveObject
        {
            private readonly IDictionary<string, string[]> presets;
            private string globalFiltersText;
            private string instanceFiltersText;
            private string statusMessage = "";

            public EditorViewModel(string                       globalFiltersTitle,
                                   string                       globalFiltersText,
                                   string                       instanceFiltersTitle,
                                   string                       instanceFiltersText,
                                   IDictionary<string, string[]> presets)
            {
                GlobalFiltersTitle = globalFiltersTitle;
                InstanceFiltersTitle = instanceFiltersTitle;
                SummaryText = "Enter one relative path per line. Global filters apply to every install for this game, while instance filters only affect the current install.";
                WarningText = "Warning: Filters exclude matching files from installation. Use them carefully to avoid partial or broken installs.";
                this.presets = presets;
                this.globalFiltersText = globalFiltersText;
                this.instanceFiltersText = instanceFiltersText;
                Presets = new ObservableCollection<PresetOption>(
                    presets.Select(kvp => new PresetOption(kvp.Key)));
            }

            public string SummaryText { get; }

            public string GlobalFiltersTitle { get; }

            public string InstanceFiltersTitle { get; }

            public string WarningText { get; }

            public ObservableCollection<PresetOption> Presets { get; }

            public string StatusMessage
            {
                get => statusMessage;
                set
                {
                    this.RaiseAndSetIfChanged(ref statusMessage, value);
                    this.RaisePropertyChanged(nameof(ShowStatusMessage));
                }
            }

            public bool ShowStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

            public bool HasPresets => Presets.Count > 0;

            public string GlobalFiltersText
            {
                get => globalFiltersText;
                set => this.RaiseAndSetIfChanged(ref globalFiltersText, value);
            }

            public string InstanceFiltersText
            {
                get => instanceFiltersText;
                set => this.RaiseAndSetIfChanged(ref instanceFiltersText, value);
            }

            public void AddPreset(string presetName)
            {
                if (!presets.TryGetValue(presetName, out var filters))
                {
                    return;
                }

                GlobalFiltersText = string.Join(Environment.NewLine,
                                                ParseFilters(GlobalFiltersText)
                                                    .Concat(filters)
                                                    .Distinct(StringComparer.OrdinalIgnoreCase));
            }

            public bool TryApply(IConfiguration configuration,
                                 GameInstance    instance,
                                 out bool        changed)
            {
                changed = false;
                var newGlobal = ParseFilters(GlobalFiltersText);
                var newInstance = ParseFilters(InstanceFiltersText);
                if (!TryValidateFilters("Global filters", newGlobal)
                    || !TryValidateFilters("Instance filters", newInstance))
                {
                    return false;
                }

                changed = !configuration.GetGlobalInstallFilters(instance.Game).SequenceEqual(newGlobal)
                          || !instance.InstallFilters.SequenceEqual(newInstance);
                if (changed)
                {
                    configuration.SetGlobalInstallFilters(instance.Game, newGlobal);
                    instance.InstallFilters = newInstance;
                }
                StatusMessage = "";
                return true;
            }

            private bool TryValidateFilters(string label,
                                            IEnumerable<string> filters)
            {
                foreach (var filter in filters)
                {
                    if (Path.IsPathRooted(filter)
                        || filter.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Any(segment => segment == "..")
                        || filter.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    {
                        StatusMessage = $"{label} must use relative game paths without parent-directory segments: {filter}";
                        return false;
                    }
                }
                return true;
            }

            private static string[] ParseFilters(string text)
                => text.Split(new[] { "\r\n", "\n", "\r" },
                              StringSplitOptions.RemoveEmptyEntries)
                       .Select(line => line.Trim())
                       .Where(line => !string.IsNullOrWhiteSpace(line))
                       .ToArray();
        }

        private sealed class PresetOption
        {
            public PresetOption(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public string ButtonLabel => $"Add {Name}";
        }
    }
}
