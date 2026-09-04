using System.Collections.Generic;
using System.Linq;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;

namespace iRacingOverlay.WPF.Widgets.MRTOneWidget;

/// <summary>
/// MRT One's configuration surface, declared rather than hand-built in XAML.
/// The widget's own appearance is untouched — this is only how it is configured.
/// </summary>
public partial class MRTOneWidget
{
    /// <summary>Field pickers for the five display slots, built from the shared registry.</summary>
    private IEnumerable<WidgetSetting> FieldSettings()
    {
        var fields = TelemetryFieldProvider.GetAllFields(includeAdvanced: true)
            .OrderBy(f => f.Category).ThenBy(f => f.DisplayName)
            .ToList();

        var names = new List<string> { "None" };
        names.AddRange(fields.Select(f => $"{f.DisplayName}  ·  {f.Category}"));

        WidgetSetting Slot(string label, string desc, SettingTier tier,
                           System.Func<TelemetryField?> get, System.Action<TelemetryField?> set) =>
            WidgetSetting.Choice(
                label, names,
                () =>
                {
                    var current = get();
                    if (current == null) return 0;
                    int i = fields.FindIndex(f => f.Field == current.Value);
                    return i < 0 ? 0 : i + 1;
                },
                i => set(i <= 0 ? null : fields[i - 1].Field),
                group: "Data fields",
                description: desc,
                tier: tier);

        yield return Slot("Top", "Value shown above the centre.", SettingTier.Basic,
            () => _dataBinding.SecondaryField,
            v => { var s = GetCurrentSettings(); s.TopField = v?.ToString();
                   UpdateDisplayFields(v, _dataBinding.PrimaryField, _dataBinding.TertiaryField);
                   UpdateWidgetSettings(s); });

        yield return Slot("Centre", "The large value in the middle of the gauge.", SettingTier.Basic,
            () => _dataBinding.PrimaryField,
            v => { var s = GetCurrentSettings(); s.CenterField = (v ?? TelemetryField.Gear).ToString();
                   UpdateDisplayFields(_dataBinding.SecondaryField, v ?? TelemetryField.Gear, _dataBinding.TertiaryField);
                   UpdateWidgetSettings(s); });

        yield return Slot("Bottom", "Value shown below the centre.", SettingTier.Basic,
            () => _dataBinding.TertiaryField,
            v => { var s = GetCurrentSettings(); s.BottomField = v?.ToString();
                   UpdateDisplayFields(_dataBinding.SecondaryField, _dataBinding.PrimaryField, v);
                   UpdateWidgetSettings(s); });

        yield return Slot("Left box", "Small readout to the left of the gauge.", SettingTier.Advanced,
            () => _leftField,
            v => { var s = GetCurrentSettings(); s.LeftField = v?.ToString();
                   UpdateSideBoxes(v, _rightField); UpdateWidgetSettings(s); });

        yield return Slot("Right box", "Small readout to the right of the gauge.", SettingTier.Advanced,
            () => _rightField,
            v => { var s = GetCurrentSettings(); s.RightField = v?.ToString();
                   UpdateSideBoxes(_leftField, v); UpdateWidgetSettings(s); });
    }

    /// <inheritdoc/>
    public override IEnumerable<WidgetSetting> GetSettings()
    {
        foreach (var s in FieldSettings()) yield return s;

        WidgetSetting Visual(string label, string desc, System.Func<MRTOneSettings, bool> get,
                             System.Action<MRTOneSettings, bool> set, SettingTier tier = SettingTier.Basic) =>
            WidgetSetting.Toggle(
                label,
                () => get(GetCurrentSettings()),
                v => { var s = GetCurrentSettings(); set(s, v); UpdateWidgetSettings(s); },
                group: "Gauge",
                description: desc,
                tier: tier);

        yield return Visual("Shift point ring", "Ring around the gauge showing the RPM band and shift window.",
            s => s.EnableShiftPointRing, (s, v) => s.EnableShiftPointRing = v);

        yield return Visual("Proximity radar", "Arc segments showing cars ahead and behind.",
            s => s.EnableEnhancedRadar, (s, v) => s.EnableEnhancedRadar = v);

        yield return Visual("Pit limiter indicator", "Flashes the gauge while the pit limiter is engaged.",
            s => s.EnablePitLimiterIndicator, (s, v) => s.EnablePitLimiterIndicator = v);

        yield return Visual("Gradient background", "Subtle gradient behind the gauge.",
            s => s.EnableGradientBackground, (s, v) => s.EnableGradientBackground = v, SettingTier.Advanced);

        yield return Visual("Glow effects", "Soft glow on the gauge ring.",
            s => s.EnableGlowEffects, (s, v) => s.EnableGlowEffects = v, SettingTier.Advanced);

        yield return Visual("Contextual auto-swap",
            "Temporarily replaces the centre value with whatever matters most — pit now, yellow flag, car alongside.",
            s => s.EnableAutoSwap, (s, v) => s.EnableAutoSwap = v, SettingTier.Advanced);

        foreach (var s in base.GetSettings()) yield return s;
    }
}
