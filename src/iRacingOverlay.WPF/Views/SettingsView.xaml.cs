using System;
using System.Windows.Controls;
using iRacingOverlay.WPF.ViewModels;

namespace iRacingOverlay.WPF.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void HotkeyCapture_HotkeyChanged(object sender, EventArgs e)
    {
        // Notify ViewModel that hotkey changed
        _viewModel.OnHotkeyChanged();
    }
}
