using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using iRacingOverlay.WPF.Controls;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;

namespace iRacingOverlay.WPF.Pages;

/// <summary>
/// The overlay screen: every widget on the left, the selected widget's settings
/// on the right.
///
/// Replaces the old Dashboard + Widgets pair. Those both listed the widgets and
/// both offered a toggle, so it was never clear which one owned the setting — and
/// the two lists were built from different sources that had drifted apart.
///
/// The settings pane is rendered from each widget's own declarations, so this page
/// contains no per-widget code at all. Adding a widget requires no change here.
/// </summary>
public partial class OverlayPage : UserControl
{
    private readonly WidgetManager _widgets;
    private readonly SettingsRenderer _renderer = new();

    private WidgetType _selected;

    /// <summary>Raised when the page changes something that should be persisted.</summary>
    public event Action? LayoutChanged;

    public OverlayPage(WidgetManager widgets)
    {
        InitializeComponent();
        _widgets = widgets;

        _renderer.Changed += () =>
        {
            _widgets.SaveCurrentLayout();
            LayoutChanged?.Invoke();
        };

        _selected = WidgetManager.SupportedWidgetTypes.FirstOrDefault();

        Loaded += (_, _) => Refresh();
    }

    /// <summary>Row shown in the widget list.</summary>
    public sealed class WidgetRow
    {
        public required string Name { get; init; }
        public required string Status { get; init; }
        public required bool IsVisible { get; init; }
        public required WidgetType Type { get; init; }
        public required Brush AccentBrush { get; init; }
    }

    /// <summary>Rebuild both panes.</summary>
    public void Refresh()
    {
        RefreshList();
        RefreshSettings();
        RefreshGlobalActions();
    }

    // ── Left: widget list ─────────────────────────────────────────────

    private void RefreshList()
    {
        var teal = (Brush)FindResource("TealBright");
        var none = Brushes.Transparent;

        var rows = new List<WidgetRow>();

        // Single source of truth for what this build ships.
        foreach (WidgetType type in Enum.GetValues<WidgetType>())
        {
            if (!WidgetManager.SupportedWidgetTypes.Contains(type)) continue;

            bool exists = _widgets.HasWidgetType(type);
            bool visible = exists && _widgets.GetWidgetsByType(type).Any(w => w.UserWantsVisible);

            rows.Add(new WidgetRow
            {
                Name = type.GetDisplayName(),
                Status = !exists ? "Not added" : visible ? "Shown" : "Hidden",
                IsVisible = visible,
                Type = type,
                AccentBrush = type == _selected ? teal : none,
            });
        }

        WidgetList.ItemsSource = rows;
    }

    private void WidgetRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not WidgetType type) return;
        if (type == _selected) return;

        _selected = type;
        TxtSearch.Text = "";
        Refresh();
    }

    private void WidgetToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box || box.Tag is not WidgetType type) return;

        bool wanted = box.IsChecked == true;

        if (wanted && !_widgets.HasWidgetType(type))
        {
            _widgets.CreateWidget(type);
        }
        else
        {
            // Hide, never remove — removing rewrites layout.json without the
            // widget, discarding its position, size and settings.
            foreach (var w in _widgets.GetWidgetsByType(type))
                w.SetUserVisibility(wanted);
        }

        _widgets.SaveCurrentLayout();

        // Selecting the widget you just switched on is almost always what you
        // want next, and it makes the right pane follow the click.
        if (wanted) _selected = type;

        Refresh();
        LayoutChanged?.Invoke();
    }

    // ── Right: settings for the selected widget ───────────────────────

    private void RefreshSettings()
    {
        TxtSelectedWidget.Text = _selected.GetDisplayName();

        var widget = _widgets.GetWidgetsByType(_selected).FirstOrDefault();

        if (widget == null)
        {
            TxtSelectedState.Text = "Not added yet — switch it on to configure it.";
            BtnRemoveWidget.IsEnabled = false;
            TxtSearch.IsEnabled = false;
            SettingsHost.Children.Clear();
            return;
        }

        BtnRemoveWidget.IsEnabled = true;
        TxtSearch.IsEnabled = true;
        TxtSelectedState.Text = widget.UserWantsVisible ? "Shown on the overlay" : "Hidden";

        var settings = AppSettings.Instance;

        _renderer.Render(
            SettingsHost,
            widget.GetSettings(),
            TxtSearch.Text,
            key => settings.ExpandedSections.TryGetValue(Key(key), out bool open) && open,
            (key, open) =>
            {
                settings.ExpandedSections[Key(key)] = open;
                settings.SaveQuiet();
            });
    }

    /// <summary>Namespace disclosure state per widget, so groups do not collide.</summary>
    private string Key(string groupKey) => $"{_selected}:{groupKey}";

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => RefreshSettings();

    private void BtnRemoveWidget_Click(object sender, RoutedEventArgs e)
    {
        var name = _selected.GetDisplayName();

        var confirm = MessageBox.Show(
            $"Remove {name}?\n\nIts position, size and settings will be discarded. "
            + "To keep them, switch it off instead.",
            "Remove widget", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.OK) return;

        foreach (var w in _widgets.GetWidgetsByType(_selected).ToList())
            _widgets.RemoveWidget(w.WidgetId);

        Refresh();
        LayoutChanged?.Invoke();
    }

    // ── Bottom-left: actions across every widget ──────────────────────

    private void RefreshGlobalActions()
    {
        var settings = AppSettings.Instance;

        BtnLockAll.Content = settings.LockWindows ? "Unlock" : "Lock";

        bool anyVisible = _widgets.ActiveWidgets.Values
            .Any(w => WidgetManager.SupportedWidgetTypes.Contains(w.WidgetType) && w.UserWantsVisible);
        BtnShowHideAll.Content = anyVisible ? "Hide all" : "Show all";

        TxtHotkeyHint.Text =
            $"{MainWindow.FormatHotkey(settings.ToggleLockModifier, settings.ToggleLockKey)} lock  ·  "
            + $"{MainWindow.FormatHotkey(settings.ToggleVisibilityModifier, settings.ToggleVisibilityKey)} show/hide";
    }

    private void BtnLockAll_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppSettings.Instance;
        settings.LockWindows = !settings.LockWindows;
        settings.Save();
        _widgets.LockAllWidgets(settings.LockWindows);
        RefreshGlobalActions();
    }

    private void BtnShowHideAll_Click(object sender, RoutedEventArgs e)
    {
        _widgets.ToggleAllWidgets();
        _widgets.SaveCurrentLayout();
        Refresh();
        LayoutChanged?.Invoke();
    }
}
