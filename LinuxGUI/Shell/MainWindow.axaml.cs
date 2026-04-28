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
        private enum BrowserColumnResizeTarget
        {
            None,
            Metadata,
            Downloads,
            Released,
            Installed,
        }

        private readonly IAppSettingsService? appSettings;
        private bool suppressHeaderInstanceSelection;
        private MainWindowViewModel? observedViewModel;
        private ContextMenu? activeModRowMenu;
        private Window? activeOwnedDialog;
        private DispatcherTimer? repositoryRefreshTimer;
        private LinuxGuiPluginController? pluginController;
        private string? pluginControllerInstanceDir;
        private BrowserColumnResizeTarget activeBrowserColumnResizeTarget;
        private double browserColumnResizeStartX;
        private double browserColumnResizeStartMetadataWidth;
        private double browserColumnResizeStartDownloadsWidth;
        private double browserColumnResizeStartReleasedWidth;
        private double browserColumnResizeStartInstalledWidth;
        private double browserColumnResizeMaxMetadataWidth;
        private bool launchUpdateCheckStarted;
        private const double OverlayWheelScrollPixels = 48;
        private const double BrowserWheelScrollPixels = 112;
        private const double QueueWheelScrollPixels = 120;
        private const double PreviewSectionWheelScrollPixels = 96;
        private const string CkanReleasesUrl = "https://github.com/KSP-CKAN/CKAN/releases";
        private static readonly IBrush PreviewConflictRowBackground = Brush.Parse("#2A1820");
        private static readonly IBrush PreviewConflictRowBorder = Brush.Parse("#3C212B");
        private static readonly IBrush PreviewConflictSelectedRowBackground = Brush.Parse("#361B24");
        private static readonly IBrush PreviewConflictSelectedRowBorder = Brush.Parse("#D95A72");

        public MainWindow()
        {
            InitializeComponent();
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CKAN-LinuxGUI/Assets/ckan.ico")));
            AddHandler(InputElement.PointerWheelChangedEvent,
                       Window_OnPointerWheelChanged,
                       RoutingStrategies.Tunnel);
            SurfaceViewToggle.AddHandler(InputElement.PointerPressedEvent,
                                         SurfaceViewToggle_OnPointerPressed,
                                         RoutingStrategies.Tunnel,
                                         true);
            Opened += OnOpened;
            Closing += OnClosing;
            PositionChanged += OnPositionChanged;
            Activated += OnActivated;
            DataContextChanged += OnDataContextChanged;
        }

        public MainWindow(MainWindowViewModel viewModel) : this(viewModel, null)
        {
        }

        public MainWindow(MainWindowViewModel viewModel,
                          IAppSettingsService? appSettingsService) : this()
        {
            DataContext = viewModel;
            appSettings = appSettingsService;
        }

    }
}
