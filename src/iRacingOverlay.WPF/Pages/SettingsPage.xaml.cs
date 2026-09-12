using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.WPF.Controls;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Utils;
using Microsoft.Win32;

namespace iRacingOverlay.WPF.Pages;

/// <summary>
/// Application settings, generated from declarations rather than hand-written.
///
/// The old page hand-wrote a card per setting and surfaced 8 of the 33 persisted
/// settings; the rest were reachable only from a widget's right-click menu, or
/// not at all. Declaring them means the page cannot drift from the model, and a
/// search box works across all of them.
///
/// Sessions folded in here as a section — it was a whole navigation destination
/// for one screen of checkboxes, and in Release it was hidden entirely while the
/// behaviour it configured still ran.
/// </summary>
public partial class SettingsPage : UserControl, IDisposable
{
    private readonly WidgetManager _widgets;
    private readonly SessionConfigService _sessionConfig;
    private readonly ProfileStorageService _profiles;
    private readonly SettingsRenderer _renderer = new();

    private readonly List<(string Key, string Title, string Description)> _sections = new()
    {
        ("general",  "General",   "Units, appearance and how the window behaves."),
        ("overlay",  "Overlay",   "When widgets show themselves, and what they alert you to."),
        ("hotkeys",  "Hotkeys",   "Global shortcuts that work while iRacing has focus."),
        ("sessions", "Sessions",  "Show different widgets depending on the session type."),
        ("data",     "Data",      "Import, export, logs and resetting."),
    };

    private string _section = "general";

    public event Action? SettingsChanged;
    public event Action? HotkeysChanged;
    public event Action? LayoutReset;

    public SettingsPage(WidgetManager widgets, SessionConfigService sessionConfig, ProfileStorageService profiles)
    {
        InitializeComponent();

        _widgets = widgets;
        _sessionConfig = sessionConfig;
        _profiles = profiles;

        _renderer.Changed += () =>
        {
            AppSettings.Instance.Save();
            SettingsChanged?.Invoke();
        };

        BuildSectionList();
        Loaded += (_, _) => Refresh();
    }

    // ── Section navigation ────────────────────────────────────────────

    private void BuildSectionList()
    {
        SectionList.Children.Clear();

        foreach (var (key, title, _) in _sections)
        {
            var button = new Button
            {
                Content = title,
                Tag = key,
                Style = (Style)FindResource("Btn.Ghost"),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 2),
            };
            button.Click += (_, _) =>
            {
                _section = key;
                TxtSearch.Text = "";
                Refresh();
            };
            SectionList.Children.Add(button);
        }
    }

    /// <summary>Rebuild the section list highlight and the content pane.</summary>
    public void Refresh()
    {
        var (_, title, description) = _sections.First(s => s.Key == _section);
        TxtSectionTitle.Text = title;
        TxtSectionDesc.Text = description;

        foreach (var child in SectionList.Children.OfType<Button>())
        {
            bool active = (string)child.Tag == _section;
            child.Foreground = active
                ? (Brush)FindResource("TealBright")
                : (Brush)FindResource("TextMuted");
            child.Background = active
                ? (Brush)FindResource("TealDim")
                : Brushes.Transparent;
        }

        // Hotkeys need press-to-capture, so that card is hand-built and shown
        // only for its own section (or when a search matches it).
        bool searching = !string.IsNullOrWhiteSpace(TxtSearch.Text);
        bool showHotkeys = _section == "hotkeys"
            || (searching && "hotkey shortcut lock show hide key bind".Contains(TxtSearch.Text.Trim().ToLowerInvariant()));

        HotkeyCard.Visibility = showHotkeys ? Visibility.Visible : Visibility.Collapsed;
        if (showHotkeys) RefreshHotkeyDisplay();

        var settings = AppSettings.Instance;

        _renderer.Render(
            SettingsHost,
            searching ? AllSettings() : SectionSettings(_section),
            TxtSearch.Text,
            key => settings.ExpandedSections.TryGetValue($"set:{key}", out bool open) && open,
            (key, open) =>
            {
                settings.ExpandedSections[$"set:{key}"] = open;
                settings.SaveQuiet();
            });
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    /// <summary>Every section's settings, for search.</summary>
    private IEnumerable<WidgetSetting> AllSettings() =>
        _sections.SelectMany(s => SectionSettings(s.Key));

    private IEnumerable<WidgetSetting> SectionSettings(string section) => section switch
    {
        "general" => GeneralSettings(),
        "overlay" => OverlaySettings(),
        "sessions" => SessionSettings(),
        "data" => DataSettings(),
        _ => Enumerable.Empty<WidgetSetting>(),
    };

    // ── General ───────────────────────────────────────────────────────

    private IEnumerable<WidgetSetting> GeneralSettings()
    {
        var s = AppSettings.Instance;

        yield return WidgetSetting.Toggle(
            "Metric units", () => s.UseMetricUnits, v => s.UseMetricUnits = v,
            group: "Units", description: "km/h, litres and °C. Off gives mph, gallons and °F.");

        yield return WidgetSetting.Slider(
            "Overlay font scale", 70, 150,
            () => s.GlobalFontScale * 100, v => s.GlobalFontScale = v / 100.0,
            group: "Appearance",
            description: "Scales the text in every overlay widget.",
            step: 5, format: WidgetSetting.Percent);

        yield return WidgetSetting.Toggle(
            "Keep this window on top", () => s.AlwaysOnTop, v => s.AlwaysOnTop = v,
            group: "Window");

        yield return WidgetSetting.Toggle(
            "Start minimised", () => s.StartMinimized, v => s.StartMinimized = v,
            group: "Window", tier: SettingTier.Advanced);

        yield return WidgetSetting.Toggle(
            "Minimise to tray", () => s.MinimizeToTray, v => s.MinimizeToTray = v,
            group: "Window", description: "Minimising hides the window to the notification area.");

        yield return WidgetSetting.Toggle(
            "Close to tray", () => s.CloseToTray, v => s.CloseToTray = v,
            group: "Window",
            description: "The X button minimises to tray instead of exiting. Use the tray menu to quit.",
            tier: SettingTier.Advanced);
    }

    // ── Overlay behaviour ─────────────────────────────────────────────

    private IEnumerable<WidgetSetting> OverlaySettings()
    {
        var s = AppSettings.Instance;

        yield return WidgetSetting.Toggle(
            "Hide outside the car", () => s.HideOutsideCar, v => s.HideOutsideCar = v,
            group: "Visibility",
            description: "Hide widgets in the garage, on the garage screen, and while a replay is playing.");

        yield return WidgetSetting.Toggle(
            "Auto-hide in the pits", () => s.AutoHideInPitsEnabled, v => s.AutoHideInPitsEnabled = v,
            group: "Auto-hide in the pits",
            description: "Hide the widgets below whenever you enter pit lane.");

        // Built from what this build actually ships, so the list can never offer
        // widgets the user cannot enable — which the hand-written version did.
        foreach (WidgetType type in Enum.GetValues<WidgetType>())
        {
            if (!WidgetManager.SupportedWidgetTypes.Contains(type)) continue;

            var key = type.ToString();
            yield return WidgetSetting.Toggle(
                type.GetDisplayName(),
                () => s.AutoHideInPitsWidgets.TryGetValue(key, out bool v) && v,
                v => s.AutoHideInPitsWidgets[key] = v,
                group: "Auto-hide in the pits",
                tier: SettingTier.Advanced);
        }
    }

    // ── Sessions ──────────────────────────────────────────────────────

    private IEnumerable<WidgetSetting> SessionSettings()
    {
        yield return WidgetSetting.Toggle(
            "Switch widgets automatically",
            () => _sessionConfig.IsEnabled,
            v => { _sessionConfig.IsEnabled = v; _sessionConfig.Save(); },
            group: "Session presets",
            description: "Show a different set of widgets for practice, qualifying and the race.");

        // One group per session type, generated from the enum rather than a
        // hand-written 26-checkbox matrix.
        foreach (SessionCategory category in Enum.GetValues<SessionCategory>())
        {
            if (category == SessionCategory.Unknown) continue;

            var preset = _sessionConfig.GetPreset(category);
            if (preset == null) continue;

            foreach (WidgetType type in Enum.GetValues<WidgetType>())
            {
                if (!WidgetManager.SupportedWidgetTypes.Contains(type)) continue;

                var captured = type;
                var capturedPreset = preset;

                yield return WidgetSetting.Toggle(
                    type.GetDisplayName(),
                    () => capturedPreset.WidgetVisibility.TryGetValue(captured, out bool v) && v,
                    v =>
                    {
                        capturedPreset.WidgetVisibility[captured] = v;
                        _sessionConfig.Save();
                    },
                    group: $"{category} session");
            }
        }

        foreach (var s in ProfileSettings()) yield return s;
    }

    // ── Profiles ──────────────────────────────────────────────────────
    //
    // A profile is a complete saved layout — every widget's position, size and
    // settings — that can be bound to a session type and a car class. The
    // presets above only switch widgets on and off; a profile moves them too.

    private IEnumerable<WidgetSetting> ProfileSettings()
    {
        yield return WidgetSetting.Note(
            "A profile is a whole layout: which widgets, where, and how each is set up. "
            + "Bind one to a session type and a car class and it takes priority over the "
            + "presets above. Cycle through them from the wheel with the hotkey.",
            group: "Profiles");

        yield return WidgetSetting.Toggle(
            "Apply the matching profile automatically",
            () => _profiles.AutoSwitch,
            v => { _profiles.AutoSwitch = v; _profiles.Save(); },
            group: "Profiles",
            description: "When the session or car changes, load the best-matching profile.");

        yield return WidgetSetting.Action(
            "Save current layout as a new profile", NewProfileFromCurrent,
            group: "Profiles",
            description: CurrentContextLabel());

        foreach (var profile in _profiles.Profiles.ToList())
        {
            var p = profile;
            string group = $"Profile: {p.Name}";
            bool active = _profiles.ActiveProfileId == p.Id;

            yield return WidgetSetting.Note(
                (active ? "Active now. " : "")
                + (p.HasLayout
                    ? $"{p.Layout.Count} widget(s) with positions and settings."
                    : "Saved before layouts were captured — holds visibility only. Save the current layout into it to upgrade."),
                group: group);

            yield return WidgetSetting.Choice(
                "Session", SessionChoices,
                () => p.SessionBinding.HasValue ? (int)p.SessionBinding.Value : 0,
                v => { p.SessionBinding = v == 0 ? null : (SessionCategory)v; _profiles.Save(); },
                group: group,
                description: "Which session type this profile is for.");

            var carChoices = CarChoicesFor(p);
            yield return WidgetSetting.Choice(
                "Car class", carChoices,
                () => Math.Max(0, carChoices.FindIndex(c => string.Equals(c, p.CarClassBinding, StringComparison.OrdinalIgnoreCase))),
                v => { p.CarClassBinding = v == 0 ? null : carChoices[v]; _profiles.Save(); },
                group: group,
                description: "Bind to the class you are in now, or leave it for any car.");

            yield return WidgetSetting.Action(
                "Apply", () => { _profiles.ApplyProfile(p.Id, _widgets); LayoutReset?.Invoke(); Refresh(); },
                group: group);

            yield return WidgetSetting.Action(
                "Save current layout into this profile", () => { _profiles.CaptureToProfile(p.Id, _widgets); Refresh(); },
                group: group,
                description: "Overwrites what the profile holds with the widgets as they are now.");

            yield return WidgetSetting.Action(
                "Rename", () => RenameProfile(p),
                group: group, tier: SettingTier.Advanced);

            if (!p.IsDefault)
            {
                yield return WidgetSetting.Action(
                    "Delete", () => { _profiles.DeleteProfile(p.Id); Refresh(); },
                    group: group, tier: SettingTier.Advanced, destructive: true);
            }
        }
    }

    private static readonly string[] SessionChoices =
        Enum.GetValues<SessionCategory>()
            .Select(c => c == SessionCategory.Unknown ? "Any session" : c.ToString())
            .ToArray();

    /// <summary>"Any car", the car detected now, and whatever the profile already names.</summary>
    private List<string> CarChoicesFor(WidgetProfile p)
    {
        var choices = new List<string> { "Any car" };
        var detected = _sessionConfig.DetectedCarClass;
        if (!string.IsNullOrEmpty(detected)) choices.Add(detected);
        if (!string.IsNullOrEmpty(p.CarClassBinding)
            && !choices.Contains(p.CarClassBinding, StringComparer.OrdinalIgnoreCase))
            choices.Add(p.CarClassBinding);
        return choices;
    }

    private string CurrentContextLabel()
    {
        var session = _sessionConfig.CurrentCategory;
        var car = _sessionConfig.DetectedCarClass;
        if (session == SessionCategory.Unknown && string.IsNullOrEmpty(car))
            return "Captures the widgets as they are now. Join a session first to bind it to a session type and car.";
        return $"Captures the widgets as they are now, bound to {(session == SessionCategory.Unknown ? "any session" : session.ToString())}"
             + $" / {(string.IsNullOrEmpty(car) ? "any car" : car)}.";
    }

    private void NewProfileFromCurrent()
    {
        var session = _sessionConfig.CurrentCategory;
        var car = _sessionConfig.DetectedCarClass;
        string suggested = session == SessionCategory.Unknown && string.IsNullOrEmpty(car)
            ? $"Profile {_profiles.Profiles.Count + 1}"
            : $"{(session == SessionCategory.Unknown ? "Any" : session.ToString())}{(string.IsNullOrEmpty(car) ? "" : " · " + car)}";

        var name = PromptDialog.Show(Window.GetWindow(this), "New profile", "Profile name", suggested);
        if (name == null) return;

        _profiles.CreateFromCurrent(name, _widgets,
            session == SessionCategory.Unknown ? null : session,
            string.IsNullOrEmpty(car) ? null : car);
        Refresh();
    }

    private void RenameProfile(WidgetProfile p)
    {
        var name = PromptDialog.Show(Window.GetWindow(this), "Rename profile", "Profile name", p.Name);
        if (name == null || name == p.Name) return;
        _profiles.RenameProfile(p.Id, name);
        Refresh();
    }

    // ── Data ──────────────────────────────────────────────────────────

    private IEnumerable<WidgetSetting> DataSettings()
    {
        yield return WidgetSetting.Action(
            "Open log folder", OpenLogs,
            group: "Diagnostics",
            description: "Logs from the last 7 days. Attach these to a bug report.");

        yield return WidgetSetting.Action(
            "Export configuration", ExportConfig,
            group: "Backup",
            description: "Save your settings and widget layout to a file.");

        yield return WidgetSetting.Action(
            "Import configuration", ImportConfig,
            group: "Backup",
            description: "Restore settings and layout from a file. Takes effect after a restart.");

        yield return WidgetSetting.Action(
            "Reset widget layout", ResetLayout,
            group: "Reset",
            description: "Puts every widget back to its default position and size. "
                       + "The current layout is backed up so this can be undone until you close MRT UI.",
            destructive: true);

        if (System.IO.File.Exists(ResetBackupPath))
        {
            yield return WidgetSetting.Action(
                "Undo layout reset", UndoReset,
                group: "Reset",
                description: "Restore the layout from before the last reset.");
        }
    }

    private static string LayoutPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI", "layout.json");

    private static string ResetBackupPath => LayoutPath + ".before-reset";

    private static void OpenLogs()
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppLog.LogDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppLog.LogDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not open the log folder", ex);
        }
    }

    private void ResetLayout()
    {
        try
        {
            if (System.IO.File.Exists(LayoutPath))
                System.IO.File.Copy(LayoutPath, ResetBackupPath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not back up layout before reset", ex);
        }

        _widgets.RemoveAllWidgets();

        // The same default set a fresh install creates. Reset previously made
        // only MRT One, leaving the user with fewer widgets than a new install.
        _widgets.CreateWidget(WidgetType.MRTOne);
        _widgets.CreateWidget(WidgetType.ProximityFeed);
        _widgets.SaveCurrentLayout();

        LayoutReset?.Invoke();
        Refresh();
    }

    private void UndoReset()
    {
        try
        {
            System.IO.File.Copy(ResetBackupPath, LayoutPath, overwrite: true);
            System.IO.File.Delete(ResetBackupPath);

            if (_widgets.LoadSavedLayout())
                LayoutReset?.Invoke();
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not restore the previous layout", ex);
            MessageBox.Show($"Could not restore the previous layout:\n{ex.Message}",
                "Undo reset", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Refresh();
    }

    private static string ConfigDir => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MRT-UI");

    private void ExportConfig()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export MRT UI configuration",
            Filter = "JSON files (*.json)|*.json",
            FileName = "MRT-UI-config.json",
            DefaultExt = ".json",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            // Flush pending debounced writes so the export captures current state.
            AppSettings.Flush();
            _widgets.FlushLayout();

            var bundle = new Dictionary<string, string>();
            var settingsPath = System.IO.Path.Combine(ConfigDir, "settings.json");
            var layoutPath = System.IO.Path.Combine(ConfigDir, "layout.json");

            if (System.IO.File.Exists(settingsPath))
                bundle["settings"] = System.IO.File.ReadAllText(settingsPath);
            if (System.IO.File.Exists(layoutPath))
                bundle["layout"] = System.IO.File.ReadAllText(layoutPath);

            System.IO.File.WriteAllText(dlg.FileName,
                System.Text.Json.JsonSerializer.Serialize(bundle,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            MessageBox.Show($"Exported to {System.IO.Path.GetFileName(dlg.FileName)}.",
                "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Config export failed", ex);
            MessageBox.Show($"Export failed:\n{ex.Message}", "Export",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportConfig()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import MRT UI configuration",
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var bundle = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                System.IO.File.ReadAllText(dlg.FileName));

            if (bundle == null)
            {
                MessageBox.Show("That file is not a valid MRT UI configuration.",
                    "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            System.IO.Directory.CreateDirectory(ConfigDir);

            if (bundle.TryGetValue("settings", out var settingsJson))
                AtomicFile.WriteAllText(System.IO.Path.Combine(ConfigDir, "settings.json"), settingsJson);
            if (bundle.TryGetValue("layout", out var layoutJson))
                AtomicFile.WriteAllText(System.IO.Path.Combine(ConfigDir, "layout.json"), layoutJson);

            MessageBox.Show("Imported. Restart MRT UI to apply.",
                "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Config import failed", ex);
            MessageBox.Show($"Import failed:\n{ex.Message}", "Import",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Hotkeys ───────────────────────────────────────────────────────

    private void RefreshHotkeyDisplay()
    {
        var s = AppSettings.Instance;
        TxtHotkeyLock.Text = MainWindow.FormatHotkey(s.ToggleLockModifier, s.ToggleLockKey);
        TxtHotkeyVisibility.Text = MainWindow.FormatHotkey(s.ToggleVisibilityModifier, s.ToggleVisibilityKey);
        TxtHotkeyProfile.Text = string.IsNullOrEmpty(s.CycleProfileKey)
            ? "Not set"
            : MainWindow.FormatHotkey(s.CycleProfileModifier, s.CycleProfileKey);

        if (s.HotkeysConflict())
        {
            TxtHotkeyStatus.Text = "Two actions are bound to the same key — change one.";
            TxtHotkeyStatus.Foreground = (Brush)FindResource("Warn");
            return;
        }

        var main = Window.GetWindow(this) as MainWindow;
        bool profileWanted = !string.IsNullOrEmpty(s.CycleProfileKey);
        if (main != null && (!main.LockHotkeyRegistered || !main.VisibilityHotkeyRegistered || (profileWanted && !main.ProfileHotkeyRegistered)))
        {
            TxtHotkeyStatus.Text = "Windows refused a binding — another application already owns it.";
            TxtHotkeyStatus.Foreground = (Brush)FindResource("Warn");
        }
        else
        {
            TxtHotkeyStatus.Text = "";
        }
    }

    /// <summary>
    /// Capture the next combination the user presses and bind it. The hotkeys used
    /// to be display-only text: the storage and the conflict check already existed,
    /// only the capture step was missing, so a conflict could not be resolved.
    /// </summary>
    private enum HotkeyAction { Lock, Visibility, Profile }

    private void CaptureHotkey(HotkeyAction action, Button trigger)
    {
        var original = trigger.Content;
        trigger.Content = "Press…";
        TxtHotkeyStatus.Text = "Press a key combination, or Esc to cancel.";
        TxtHotkeyStatus.Foreground = (Brush)FindResource("TealBright");

        var window = Window.GetWindow(this);
        if (window == null) { trigger.Content = original; return; }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Wait for the actual key, not the modifier being held.
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                return;

            e.Handled = true;
            window.PreviewKeyDown -= OnKeyDown;
            trigger.Content = original;

            if (key == Key.Escape) { RefreshHotkeyDisplay(); return; }

            string modifier =
                (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? "Ctrl" :
                (Keyboard.Modifiers & ModifierKeys.Alt) != 0 ? "Alt" :
                (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? "Shift" : "None";

            var s = AppSettings.Instance;
            switch (action)
            {
                case HotkeyAction.Lock:
                    s.ToggleLockModifier = modifier;
                    s.ToggleLockKey = key.ToString();
                    break;
                case HotkeyAction.Visibility:
                    s.ToggleVisibilityModifier = modifier;
                    s.ToggleVisibilityKey = key.ToString();
                    break;
                case HotkeyAction.Profile:
                    s.CycleProfileModifier = modifier;
                    s.CycleProfileKey = key.ToString();
                    break;
            }
            s.Save();

            // Re-register immediately so a refusal is reported straight away.
            HotkeysChanged?.Invoke();
            RefreshHotkeyDisplay();
        }

        window.PreviewKeyDown += OnKeyDown;
    }

    private void BtnRebindLock_Click(object sender, RoutedEventArgs e) =>
        CaptureHotkey(HotkeyAction.Lock, (Button)sender);

    private void BtnRebindVisibility_Click(object sender, RoutedEventArgs e) =>
        CaptureHotkey(HotkeyAction.Visibility, (Button)sender);

    private void BtnRebindProfile_Click(object sender, RoutedEventArgs e) =>
        CaptureHotkey(HotkeyAction.Profile, (Button)sender);

    private void BtnClearProfileHotkey_Click(object sender, RoutedEventArgs e)
    {
        var s = AppSettings.Instance;
        s.CycleProfileKey = "";
        s.Save();
        HotkeysChanged?.Invoke();
        RefreshHotkeyDisplay();
    }

    public void Dispose() { }
}
