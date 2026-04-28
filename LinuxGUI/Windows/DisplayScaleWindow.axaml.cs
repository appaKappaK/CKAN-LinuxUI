using Avalonia.Controls;

namespace CKAN.LinuxGUI
{
    public partial class DisplayScaleWindow : Window
    {
        public DisplayScaleWindow()
        {
            InitializeComponent();
        }

        public DisplayScaleWindow(MainWindowViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void CloseButton_OnClick(object? sender,
                                         Avalonia.Interactivity.RoutedEventArgs e)
            => Close();
    }
}
