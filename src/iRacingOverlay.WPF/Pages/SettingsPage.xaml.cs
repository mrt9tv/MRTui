using System.Windows;
using System.Windows.Controls;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Pages;

public partial class SettingsPage : UserControl
{
    private bool _suppressControlEvents = true;

    /// <summary>
    /// Raised when AlwaysOnTop or MinimizeToTray changes so MainWindow can respond.
    /// </summary>
    public event System.Action? SettingsChanged;

    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
        _suppressControlEvents = false;
    }

    private void LoadSettings()
    {
        var s = AppSettings.Instance;
        SliderGlobalFontScale.Value = s.GlobalFontScale * 100;
        TxtGlobalFontScale.Text = $"{(int)(s.GlobalFontScale * 100)}%";
        ChkAlwaysOnTop.IsChecked = s.AlwaysOnTop;
        ChkStartMinimized.IsChecked = s.StartMinimized;
        ChkMinimizeToTray.IsChecked = s.MinimizeToTray;
        ChkMetricUnits.IsChecked = s.UseMetricUnits;

        string FormatMod(string m) => m == "None" ? "" : m;
        string lockMod = FormatMod(s.ToggleLockModifier);
        string lockKey = s.ToggleLockKey;
        TxtHotkeyLock.Text = string.IsNullOrEmpty(lockMod) ? lockKey : $"{lockMod}+{lockKey}";

        string visMod = FormatMod(s.ToggleVisibilityModifier);
        string visKey = s.ToggleVisibilityKey;
        TxtHotkeyVisibility.Text = string.IsNullOrEmpty(visMod) ? visKey : $"{visMod}+{visKey}";
    }

    private void GlobalFontScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressControlEvents || TxtGlobalFontScale == null) return;
        int pct = (int)e.NewValue;
        TxtGlobalFontScale.Text = $"{pct}%";
        AppSettings.Instance.GlobalFontScale = pct / 100.0;
        AppSettings.Instance.Save();
    }

    private void WindowBehavior_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var s = AppSettings.Instance;
        s.AlwaysOnTop = ChkAlwaysOnTop.IsChecked == true;
        s.StartMinimized = ChkStartMinimized.IsChecked == true;
        s.MinimizeToTray = ChkMinimizeToTray.IsChecked == true;
        s.Save();
        SettingsChanged?.Invoke();
    }

    private void Units_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        AppSettings.Instance.UseMetricUnits = ChkMetricUnits.IsChecked == true;
        AppSettings.Instance.Save();
    }
}
