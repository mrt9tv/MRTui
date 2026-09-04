using System;
using System.Collections.Generic;

namespace iRacingOverlay.WPF.Models;

/// <summary>How prominent a setting is. Advanced ones live behind a disclosure.</summary>
public enum SettingTier
{
    /// <summary>Shown immediately — the settings most people change.</summary>
    Basic,

    /// <summary>Behind "More options".</summary>
    Advanced,
}

/// <summary>What kind of control renders a setting.</summary>
public enum SettingKind
{
    Toggle,
    Slider,
    Choice,
    Action,
    Note,
}

/// <summary>
/// One configurable setting, declared by the widget that owns it.
///
/// The Widgets page used to hand-write a XAML panel per widget — 854 lines, with
/// each widget's controls split across two grid columns, and eight separate
/// hand-rolled opacity sliders for what is a single WidgetBase property. Adding a
/// widget meant editing eight places.
///
/// Declaring settings instead means one renderer draws them all, universal
/// settings are defined once, search works across everything, and adding a widget
/// is one method.
/// </summary>
public sealed class WidgetSetting
{
    public required SettingKind Kind { get; init; }

    /// <summary>Short label shown next to the control.</summary>
    public required string Label { get; init; }

    /// <summary>Optional one-line explanation, shown as a tooltip and matched by search.</summary>
    public string? Description { get; init; }

    /// <summary>Group heading this setting sits under, e.g. "Appearance".</summary>
    public string Group { get; init; } = "General";

    public SettingTier Tier { get; init; } = SettingTier.Basic;

    // ── Toggle ────────────────────────────────────────────────────────
    public Func<bool>? GetBool { get; init; }
    public Action<bool>? SetBool { get; init; }

    // ── Slider ────────────────────────────────────────────────────────
    public Func<double>? GetValue { get; init; }
    public Action<double>? SetValue { get; init; }
    public double Min { get; init; }
    public double Max { get; init; } = 100;
    public double Step { get; init; } = 1;

    /// <summary>Formats the numeric value for the readout, e.g. "85%".</summary>
    public Func<double, string>? FormatValue { get; init; }

    // ── Choice ────────────────────────────────────────────────────────
    public IReadOnlyList<string>? Choices { get; init; }
    public Func<int>? GetChoice { get; init; }
    public Action<int>? SetChoice { get; init; }

    // ── Action ────────────────────────────────────────────────────────
    public Action? Invoke { get; init; }

    /// <summary>Actions that discard data get a confirmation and a distinct style.</summary>
    public bool IsDestructive { get; init; }

    /// <summary>
    /// Everything searchable about this setting, lowercased once at build time.
    /// </summary>
    public string SearchText =>
        _searchText ??= ($"{Label} {Description} {Group}").ToLowerInvariant();

    private string? _searchText;

    // ── Factory helpers ───────────────────────────────────────────────
    // Keep declarations at the call site short enough to read as a list.

    public static WidgetSetting Toggle(
        string label, Func<bool> get, Action<bool> set,
        string group = "General", string? description = null,
        SettingTier tier = SettingTier.Basic) =>
        new()
        {
            Kind = SettingKind.Toggle,
            Label = label,
            Description = description,
            Group = group,
            Tier = tier,
            GetBool = get,
            SetBool = set,
        };

    public static WidgetSetting Slider(
        string label, double min, double max,
        Func<double> get, Action<double> set,
        string group = "General", string? description = null,
        SettingTier tier = SettingTier.Basic,
        double step = 1, Func<double, string>? format = null) =>
        new()
        {
            Kind = SettingKind.Slider,
            Label = label,
            Description = description,
            Group = group,
            Tier = tier,
            Min = min,
            Max = max,
            Step = step,
            GetValue = get,
            SetValue = set,
            FormatValue = format,
        };

    public static WidgetSetting Choice(
        string label, IReadOnlyList<string> choices,
        Func<int> get, Action<int> set,
        string group = "General", string? description = null,
        SettingTier tier = SettingTier.Basic) =>
        new()
        {
            Kind = SettingKind.Choice,
            Label = label,
            Description = description,
            Group = group,
            Tier = tier,
            Choices = choices,
            GetChoice = get,
            SetChoice = set,
        };

    public static WidgetSetting Action(
        string label, Action invoke,
        string group = "General", string? description = null,
        SettingTier tier = SettingTier.Basic,
        bool destructive = false) =>
        new()
        {
            Kind = SettingKind.Action,
            Label = label,
            Description = description,
            Group = group,
            Tier = tier,
            Invoke = invoke,
            IsDestructive = destructive,
        };

    public static WidgetSetting Note(string text, string group = "General") =>
        new()
        {
            Kind = SettingKind.Note,
            Label = text,
            Group = group,
        };

    /// <summary>Percentage formatter, the most common case.</summary>
    public static string Percent(double v) => $"{(int)v}%";

    /// <summary>Pixel formatter.</summary>
    public static string Pixels(double v) => $"{(int)v}px";
}
