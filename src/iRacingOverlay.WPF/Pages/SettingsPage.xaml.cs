using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    /// <summary>
    /// Raised when the user undoes a layout reset — MainWindow should reload
    /// the restored layout.json.
    /// </summary>
    public event System.Action? UndoResetRequested;

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
        BuildAutoHideWidgetList(s);

        RefreshHotkeyDisplay();
    }

    /// <summary>
    /// Build the auto-hide checkbox list from the widgets this build actually ships,
    /// rather than a hardcoded list that offered widgets the user cannot enable.
    /// </summary>
    private void BuildAutoHideWidgetList(AppSettings s)
    {
        AutoHideWidgetList.Children.Clear();

        foreach (WidgetType wt in System.Enum.GetValues<WidgetType>())
        {
            if (!Services.WidgetManager.SupportedWidgetTypes.Contains(wt)) continue;

            var key = wt.ToString();
            var chk = new CheckBox
            {
                Content = wt.GetDisplayName(),
                Style = FindResource("MRT.ToggleButton") as Style,
                Margin = new Thickness(0, 0, 10, 4),
                Tag = key,
                IsChecked = s.AutoHideInPitsWidgets.TryGetValue(key, out bool v) && v,
            };
            chk.Checked += AutoHideWidget_Changed;
            chk.Unchecked += AutoHideWidget_Changed;
            AutoHideWidgetList.Children.Add(chk);
        }
    }

    private void RefreshHotkeyDisplay()
    {
        var s = AppSettings.Instance;
        TxtHotkeyLock.Text = MainWindow.FormatHotkey(s.ToggleLockModifier, s.ToggleLockKey);
        TxtHotkeyVisibility.Text = MainWindow.FormatHotkey(s.ToggleVisibilityModifier, s.ToggleVisibilityKey);

        if (s.HotkeysConflict())
        {
            TxtHotkeyStatus.Text = "Both actions are bound to the same key — change one.";
            TxtHotkeyStatus.Foreground = FindResource("OrangePrimary") as Brush ?? Brushes.Orange;
            return;
        }

        var main = Window.GetWindow(this) as MainWindow;
        if (main != null && (!main.LockHotkeyRegistered || !main.VisibilityHotkeyRegistered))
        {
            TxtHotkeyStatus.Text = "Windows refused a binding — another application already owns it.";
            TxtHotkeyStatus.Foreground = FindResource("OrangePrimary") as Brush ?? Brushes.Orange;
        }
        else
        {
            TxtHotkeyStatus.Text = "";
        }
    }

    // ── Hotkey rebinding ────────────────────────────────────────────

    /// <summary>
    /// Capture the next key combination the user presses and bind it.
    /// The hotkeys were previously display-only: AppSettings already stored the
    /// modifier and key, and HotkeysConflict() already existed — only the capture
    /// step was missing, leaving the user no way to resolve a conflict.
    /// </summary>
    private void CaptureHotkey(bool forLockAction, Button trigger)
    {
        var original = trigger.Content;
        trigger.Content = "Press…";
        TxtHotkeyStatus.Text = "Press a key combination, or Esc to cancel.";
        TxtHotkeyStatus.Foreground = FindResource("TealPrimary") as Brush ?? Brushes.Teal;

        var window = Window.GetWindow(this);
        if (window == null) { trigger.Content = original; return; }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore bare modifier presses — wait for the actual key.
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                return;

            e.Handled = true;
            window.PreviewKeyDown -= OnKeyDown;
            trigger.Content = original;

            if (key == Key.Escape)
            {
                RefreshHotkeyDisplay();
                return;
            }

            string modifier =
                (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? "Ctrl" :
                (Keyboard.Modifiers & ModifierKeys.Alt) != 0 ? "Alt" :
                (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? "Shift" : "None";

            var s = AppSettings.Instance;
            if (forLockAction)
            {
                s.ToggleLockModifier = modifier;
                s.ToggleLockKey = key.ToString();
            }
            else
            {
                s.ToggleVisibilityModifier = modifier;
                s.ToggleVisibilityKey = key.ToString();
            }
            s.Save();

            // Re-register immediately so the new binding takes effect and any
            // failure is reported straight away.
            (window as MainWindow)?.RegisterGlobalHotkeys();
            RefreshHotkeyDisplay();
        }

        window.PreviewKeyDown += OnKeyDown;
    }

    private void BtnRebindLock_Click(object sender, RoutedEventArgs e) =>
        CaptureHotkey(forLockAction: true, (Button)sender);

    private void BtnRebindVisibility_Click(object sender, RoutedEventArgs e) =>
        CaptureHotkey(forLockAction: false, (Button)sender);

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
        if (sender is not CheckBox chk || chk.Tag is not string key) return;

        AppSettings.Instance.AutoHideInPitsWidgets[key] = chk.IsChecked == true;
        AppSettings.Instance.Save();
    }

    // ── Layout reset ────────────────────────────────────────────────

    private static string LayoutPath => System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
        "MRT-UI", "layout.json");

    private static string ResetBackupPath => LayoutPath + ".before-reset";

    private void BtnResetLayout_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will reset all widget positions and sizes to defaults.\n\n" +
            "Your current layout will be saved so you can undo this until you close MRT UI.\n\nContinue?",
            "Reset Layout", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        // Keep a copy so the reset is undoable — it used to be immediate and final.
        try
        {
            if (System.IO.File.Exists(LayoutPath))
                System.IO.File.Copy(LayoutPath, ResetBackupPath, overwrite: true);
        }
        catch (System.Exception ex)
        {
            Utils.AppLog.Warn("Could not back up layout before reset", ex);
        }

        try
        {
            if (System.IO.File.Exists(LayoutPath))
                System.IO.File.Delete(LayoutPath);
        }
        catch (System.Exception ex)
        {
            Utils.AppLog.Warn("Could not delete layout during reset", ex);
        }

        ResetLayoutRequested?.Invoke();
        BtnUndoReset.Visibility = Visibility.Visible;
        TxtConfigStatus.Text = "";
    }

    private void BtnUndoReset_Click(object sender, RoutedEventArgs e)
    {
        if (!System.IO.File.Exists(ResetBackupPath))
        {
            BtnUndoReset.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            System.IO.File.Copy(ResetBackupPath, LayoutPath, overwrite: true);
            System.IO.File.Delete(ResetBackupPath);
            BtnUndoReset.Visibility = Visibility.Collapsed;
            UndoResetRequested?.Invoke();
        }
        catch (System.Exception ex)
        {
            Utils.AppLog.Error("Could not restore layout", ex);
            MessageBox.Show($"Could not restore the previous layout:\n{ex.Message}",
                "Undo Reset", MessageBoxButton.OK, MessageBoxImage.Error);
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
