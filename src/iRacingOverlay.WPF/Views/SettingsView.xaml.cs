using System.Windows.Controls;
using iRacingOverlay.WPF.ViewModels;

namespace iRacingOverlay.WPF.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
