using System.Windows.Controls;
using iRacingOverlay.WPF.ViewModels;

namespace iRacingOverlay.WPF.Views;

public partial class OverlayView : UserControl
{
    public OverlayView(OverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
