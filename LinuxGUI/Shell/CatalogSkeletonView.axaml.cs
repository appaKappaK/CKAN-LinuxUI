using Avalonia;
using Avalonia.Controls;

namespace CKAN.LinuxGUI
{
    public partial class CatalogSkeletonView : UserControl
    {
        public static readonly StyledProperty<string> CountLabelProperty =
            AvaloniaProperty.Register<CatalogSkeletonView, string>(nameof(CountLabel), "Loading…");

        public static readonly StyledProperty<string> FiltersLabelProperty =
            AvaloniaProperty.Register<CatalogSkeletonView, string>(nameof(FiltersLabel), "More Filters");

        public CatalogSkeletonView()
        {
            InitializeComponent();
        }

        public string CountLabel
        {
            get => GetValue(CountLabelProperty);
            set => SetValue(CountLabelProperty, value);
        }

        public string FiltersLabel
        {
            get => GetValue(FiltersLabelProperty);
            set => SetValue(FiltersLabelProperty, value);
        }
    }
}
