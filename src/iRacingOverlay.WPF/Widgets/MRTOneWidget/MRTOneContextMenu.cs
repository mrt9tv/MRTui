using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;

namespace iRacingOverlay.WPF.Widgets.MRTOneWidget;

/// <summary>
/// Builds the right-click context menu for the MRT One widget.
/// Provides intuitive access to:
///   - Data field swapping for each slot (top, center, bottom, left, right)
///   - Screen centering (horizontal / vertical / both)
///   - Section visibility toggles
///
/// MODULAR: Uses TelemetryFieldProvider as the shared field registry so any
/// future widget can reuse the same field list and categories.
/// </summary>
public static class MRTOneContextMenu
{
    /// <summary>
    /// Build the full context menu for the MRT One widget.
    /// </summary>
    public static ContextMenu Build(
        MRTOneSettings settings,
        Action<string, TelemetryField?> onFieldChanged,
        Action onCenterH,
        Action onCenterV,
        Action onCenterBoth,
        Action<MRTOneSettings> onSettingsChanged)
    {
        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 128, 128)),
            BorderThickness = new Thickness(1)
        };

        // ─── Data Fields ────────────────────────────────────────────
        var dataHeader = new MenuItem
        {
            Header = "DATA FIELDS",
            IsEnabled = false,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 180, 180)),
            FontWeight = FontWeights.Bold,
            FontSize = 11
        };
        menu.Items.Add(dataHeader);

        menu.Items.Add(BuildFieldSubmenu("Top Field", "top", settings.TopField, false, onFieldChanged));
        menu.Items.Add(BuildFieldSubmenu("Center Field", "center", settings.CenterField, false, onFieldChanged));
        menu.Items.Add(BuildFieldSubmenu("Bottom Field", "bottom", settings.BottomField, false, onFieldChanged));
        menu.Items.Add(BuildFieldSubmenu("Left Field", "left", settings.LeftField, true, onFieldChanged));
        menu.Items.Add(BuildFieldSubmenu("Right Field", "right", settings.RightField, true, onFieldChanged));

        menu.Items.Add(new Separator());

        // ─── Layout ────────────────────────────────────────────────
        var layoutHeader = new MenuItem
        {
            Header = "LAYOUT",
            IsEnabled = false,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 180, 180)),
            FontWeight = FontWeights.Bold,
            FontSize = 11
        };
        menu.Items.Add(layoutHeader);

        var centerH = new MenuItem { Header = "Center Horizontally" };
        centerH.Click += (_, _) => onCenterH();
        menu.Items.Add(centerH);

        var centerV = new MenuItem { Header = "Center Vertically" };
        centerV.Click += (_, _) => onCenterV();
        menu.Items.Add(centerV);

        var centerBothItem = new MenuItem { Header = "Center Both" };
        centerBothItem.Click += (_, _) => onCenterBoth();
        menu.Items.Add(centerBothItem);

        menu.Items.Add(new Separator());

        // ─── Sections ──────────────────────────────────────────────
        var sectionsHeader = new MenuItem
        {
            Header = "SECTIONS",
            IsEnabled = false,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 180, 180)),
            FontWeight = FontWeights.Bold,
            FontSize = 11
        };
        menu.Items.Add(sectionsHeader);

        menu.Items.Add(BuildToggleItem("Show Top", settings.ShowTop, v =>
        {
            settings.ShowTop = v;
            onSettingsChanged(settings);
        }));
        menu.Items.Add(BuildToggleItem("Show Center", settings.ShowCenter, v =>
        {
            settings.ShowCenter = v;
            onSettingsChanged(settings);
        }));
        menu.Items.Add(BuildToggleItem("Show Bottom", settings.ShowBottom, v =>
        {
            settings.ShowBottom = v;
            onSettingsChanged(settings);
        }));
        menu.Items.Add(BuildToggleItem("Show Left", settings.ShowLeft, v =>
        {
            settings.ShowLeft = v;
            onSettingsChanged(settings);
        }));
        menu.Items.Add(BuildToggleItem("Show Right", settings.ShowRight, v =>
        {
            settings.ShowRight = v;
            onSettingsChanged(settings);
        }));

        menu.Items.Add(new Separator());

        // ─── Visual Effects ────────────────────────────────────────
        var visualHeader = new MenuItem
        {
            Header = "VISUALS",
            IsEnabled = false,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 180, 180)),
            FontWeight = FontWeights.Bold,
            FontSize = 11
        };
        menu.Items.Add(visualHeader);

        menu.Items.Add(BuildToggleItem("Gradient Background", settings.EnableGradientBackground, v =>
        {
            settings.EnableGradientBackground = v;
            onSettingsChanged(settings);
        }));
        menu.Items.Add(BuildToggleItem("Shift Point Ring", settings.EnableShiftPointRing, v =>
        {
            settings.EnableShiftPointRing = v;
            onSettingsChanged(settings);
        }));
        menu.Items.Add(BuildToggleItem("Glow Effects", settings.EnableGlowEffects, v =>
        {
            settings.EnableGlowEffects = v;
            onSettingsChanged(settings);
        }));
        menu.Items.Add(BuildToggleItem("Fuel Display", settings.EnableFuelDisplay, v =>
        {
            settings.EnableFuelDisplay = v;
            onSettingsChanged(settings);
        }));
        menu.Items.Add(BuildToggleItem("Enhanced Radar", settings.EnableEnhancedRadar, v =>
        {
            settings.EnableEnhancedRadar = v;
            onSettingsChanged(settings);
        }));

        ApplyMenuStyle(menu);
        return menu;
    }

    /// <summary>
    /// Build a submenu for selecting a telemetry field for a specific slot.
    /// Fields are grouped by category for intuitive navigation.
    /// </summary>
    private static MenuItem BuildFieldSubmenu(
        string label,
        string slot,
        string? currentField,
        bool allowNone,
        Action<string, TelemetryField?> onFieldChanged)
    {
        var currentDisplay = string.IsNullOrEmpty(currentField) ? "None" : currentField;
        var submenu = new MenuItem
        {
            Header = $"{label}:  {currentDisplay}"
        };

        // "None" option for side slots
        if (allowNone)
        {
            var noneItem = new MenuItem
            {
                Header = string.IsNullOrEmpty(currentField) ? "✓ None" : "  None"
            };
            noneItem.Click += (_, _) => onFieldChanged(slot, null);
            submenu.Items.Add(noneItem);
            submenu.Items.Add(new Separator());
        }

        // Group fields by category
        var categories = TelemetryFieldProvider.GetFieldsByCategory(includeAdvanced: true);
        foreach (var (category, fields) in categories)
        {
            var catHeader = new MenuItem
            {
                Header = category.ToUpperInvariant(),
                IsEnabled = false,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 160, 160)),
                FontSize = 10
            };
            submenu.Items.Add(catHeader);

            foreach (var field in fields)
            {
                bool isSelected = field.Field.ToString().Equals(currentField, StringComparison.OrdinalIgnoreCase);
                var item = new MenuItem
                {
                    Header = isSelected ? $"✓ {field.DisplayName}" : $"  {field.DisplayName}",
                    ToolTip = field.Description
                };
                item.Click += (_, _) => onFieldChanged(slot, field.Field);
                submenu.Items.Add(item);
            }

            submenu.Items.Add(new Separator());
        }

        // Remove trailing separator
        if (submenu.Items.Count > 0 && submenu.Items[^1] is Separator)
            submenu.Items.RemoveAt(submenu.Items.Count - 1);

        return submenu;
    }

    /// <summary>
    /// Build a checkable toggle menu item
    /// </summary>
    private static MenuItem BuildToggleItem(string label, bool currentValue, Action<bool> onToggle)
    {
        var item = new MenuItem
        {
            Header = currentValue ? $"✓ {label}" : $"   {label}",
            IsCheckable = false // We handle the visual ourselves for cleaner UX
        };
        item.Click += (_, _) => onToggle(!currentValue);
        return item;
    }

    /// <summary>
    /// Apply consistent dark theme styling to the context menu and all children
    /// </summary>
    private static void ApplyMenuStyle(ContextMenu menu)
    {
        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(MenuItem.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(35, 35, 35))));
        style.Setters.Add(new Setter(MenuItem.ForegroundProperty,
            new SolidColorBrush(Color.FromRgb(220, 220, 220))));
        style.Setters.Add(new Setter(MenuItem.FontFamilyProperty,
            new FontFamily("Consolas")));
        style.Setters.Add(new Setter(MenuItem.FontSizeProperty, 12.0));

        // Hover trigger
        var hoverTrigger = new Trigger
        {
            Property = MenuItem.IsHighlightedProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(0, 80, 80))));
        style.Triggers.Add(hoverTrigger);

        menu.Resources[typeof(MenuItem)] = style;

        // Separator style
        var sepStyle = new Style(typeof(Separator));
        sepStyle.Setters.Add(new Setter(Separator.BackgroundProperty,
            new SolidColorBrush(Color.FromRgb(60, 60, 60))));
        sepStyle.Setters.Add(new Setter(Separator.MarginProperty,
            new Thickness(4, 2, 4, 2)));
        menu.Resources[typeof(Separator)] = sepStyle;
    }
}
