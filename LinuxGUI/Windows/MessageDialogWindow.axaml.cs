using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CKAN.LinuxGUI
{
    public partial class MessageDialogWindow : Window
    {
        public MessageDialogWindow()
            : this("CKAN Linux", "")
        {
        }

        public MessageDialogWindow(string title,
                                   string message)
        {
            InitializeComponent();
            DataContext = new WindowViewModel(title, message);
        }

        private void CloseButton_OnClick(object? sender,
                                         RoutedEventArgs e)
            => Close();

        private sealed class WindowViewModel
        {
            public WindowViewModel(string title,
                                   string message)
            {
                Title = title;
                Message = message;
            }

            public string Title { get; }

            public string Message { get; }
        }
    }
}
