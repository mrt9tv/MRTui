using System.Windows;
using System.Windows.Controls;
using iRacingOverlay.WPF.ViewModels;

namespace iRacingOverlay.WPF.Views;

public partial class OverlayView : System.Windows.Controls.UserControl
{
    public OverlayView(OverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void WidgetItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is WidgetItemViewModel widget && DataContext is OverlayViewModel vm)
        {
            vm.SelectedWidget = widget;
        }
    }
}
