using System.Windows;
using System.Windows.Controls;
using iRacingOverlay.WPF.Models;
using Microsoft.Win32;

namespace iRacingOverlay.WPF.Pages;

public partial class SettingsPage : UserControl
{
    private bool _suppressControlEvents = true;

    /// <summary>
    /// Raised when AlwaysOnTop or MinimizeToTray changes so MainWindow can respond.
    /// </summary>
    public event System.Action? SettingsChanged;

    /// <summary>
    /// Raised when the user clicks Reset Layout — MainWindow should recreate default widgets.
    /// </summary>
    public event System.Action? ResetLayoutRequested;

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
        ChkCloseToTray.IsChecked = s.CloseToTray;
        ChkMetricUnits.IsChecked = s.UseMetricUnits;

        // Auto-hide in pits
        ChkAutoHideInPits.IsChecked = s.AutoHideInPitsEnabled;
        PanelAutoHideWidgets.IsEnabled = s.AutoHideInPitsEnabled;
        PanelAutoHideWidgets.Opacity = s.AutoHideInPitsEnabled ? 1.0 : 0.4;
        LoadAutoHideWidget(ChkAutoHideMRTOne, "MRTOne", s);
        LoadAutoHideWidget(ChkAutoHideProxFeed, "ProximityFeed", s);
        LoadAutoHideWidget(ChkAutoHideFuelCalc, "FuelCalculator", s);
        LoadAutoHideWidget(ChkAutoHideStandings, "Standings", s);
        LoadAutoHideWidget(ChkAutoHideRelative, "Relative", s);

        string FormatMod(string m) => m == "None" ? "" : m;
        string lockMod = FormatMod(s.ToggleLockModifier);
        string lockKey = s.ToggleLockKey;
        TxtHotkeyLock.Text = string.IsNullOrEmpty(lockMod) ? lockKey : $"{lockMod}+{lockKey}";

        string visMod = FormatMod(s.ToggleVisibilityModifier);
        string visKey = s.ToggleVisibilityKey;
        TxtHotkeyVisibility.Text = string.IsNullOrEmpty(visMod) ? visKey : $"{visMod}+{visKey}";
    }

    private static void LoadAutoHideWidget(CheckBox chk, string widgetKey, AppSettings s)
    {
        chk.IsChecked = s.AutoHideInPitsWidgets.TryGetValue(widgetKey, out bool v) && v;
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
        s.CloseToTray = ChkCloseToTray.IsChecked == true;
        s.Save();
        SettingsChanged?.Invoke();
    }

    private void Units_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        AppSettings.Instance.UseMetricUnits = ChkMetricUnits.IsChecked == true;
        AppSettings.Instance.Save();
    }

    // ── Auto-hide in pits ───────────────────────────────────────────

    private void AutoHideInPits_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var s = AppSettings.Instance;
        s.AutoHideInPitsEnabled = ChkAutoHideInPits.IsChecked == true;
        PanelAutoHideWidgets.IsEnabled = s.AutoHideInPitsEnabled;
        PanelAutoHideWidgets.Opacity = s.AutoHideInPitsEnabled ? 1.0 : 0.4;
        s.Save();
    }

    private void AutoHideWidget_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        var s = AppSettings.Instance;
        s.AutoHideInPitsWidgets["MRTOne"] = ChkAutoHideMRTOne.IsChecked == true;
        s.AutoHideInPitsWidgets["ProximityFeed"] = ChkAutoHideProxFeed.IsChecked == true;
        s.AutoHideInPitsWidgets["FuelCalculator"] = ChkAutoHideFuelCalc.IsChecked == true;
        s.AutoHideInPitsWidgets["Standings"] = ChkAutoHideStandings.IsChecked == true;
        s.AutoHideInPitsWidgets["Relative"] = ChkAutoHideRelative.IsChecked == true;
        s.Save();
    }

    // ── Layout reset ────────────────────────────────────────────────

    private void BtnResetLayout_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will reset all widget positions and sizes to defaults.\nYour current layout will be lost.\n\nContinue?",
            "Reset Layout", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // Delete the saved layout file
            var layoutPath = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "MRT-UI", "layout.json");
            try
            {
                if (System.IO.File.Exists(layoutPath))
                    System.IO.File.Delete(layoutPath);
            }
            catch { /* ignore */ }

            ResetLayoutRequested?.Invoke();
        }
    }

    // ── Config export / import ──────────────────────────────────────

    private void BtnExportConfig_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export MRT UI Configuration",
            Filter = "JSON files (*.json)|*.json",
            FileName = "MRT-UI-config.json",
            DefaultExt = ".json"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var configDir = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                    "MRT-UI");

                // Bundle settings + layout into one JSON
                var settingsPath = System.IO.Path.Combine(configDir, "settings.json");
                var layoutPath = System.IO.Path.Combine(configDir, "layout.json");

                var bundle = new System.Collections.Generic.Dictionary<string, string>();
                if (System.IO.File.Exists(settingsPath))
                    bundle["settings"] = System.IO.File.ReadAllText(settingsPath);
                if (System.IO.File.Exists(layoutPath))
                    bundle["layout"] = System.IO.File.ReadAllText(layoutPath);

                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(dlg.FileName,
                    System.Text.Json.JsonSerializer.Serialize(bundle, options));

                TxtConfigStatus.Text = $"Exported to {System.IO.Path.GetFileName(dlg.FileName)}";
            }
            catch (System.Exception ex)
            {
                TxtConfigStatus.Text = $"Export failed: {ex.Message}";
            }
        }
    }

    private void BtnImportConfig_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import MRT UI Configuration",
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var json = System.IO.File.ReadAllText(dlg.FileName);
                var bundle = System.Text.Json.JsonSerializer.Deserialize<
                    System.Collections.Generic.Dictionary<string, string>>(json);

                if (bundle == null)
                {
                    TxtConfigStatus.Text = "Invalid config file.";
                    return;
                }

                var configDir = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                    "MRT-UI");
                System.IO.Directory.CreateDirectory(configDir);

                if (bundle.TryGetValue("settings", out var settingsJson))
                    System.IO.File.WriteAllText(System.IO.Path.Combine(configDir, "settings.json"), settingsJson);
                if (bundle.TryGetValue("layout", out var layoutJson))
                    System.IO.File.WriteAllText(System.IO.Path.Combine(configDir, "layout.json"), layoutJson);

                TxtConfigStatus.Text = "Imported! Restart MRT UI to apply.";
            }
            catch (System.Exception ex)
            {
                TxtConfigStatus.Text = $"Import failed: {ex.Message}";
            }
        }
    }
}
