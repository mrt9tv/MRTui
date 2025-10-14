using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using iRacingOverlay.WPF.ViewModels;

namespace iRacingOverlay.WPF.Views;

public partial class DashboardView : Page
{
    private readonly DashboardViewModel _viewModel;

    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void ManageWidgets_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Overlay Manager page
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToOverlayPage();
        }
    }
}
