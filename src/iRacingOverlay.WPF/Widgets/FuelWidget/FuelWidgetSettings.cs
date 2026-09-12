using System.Collections.Generic;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.FuelWidget;

public partial class FuelWidget
{
    /// <inheritdoc/>
    public override IEnumerable<WidgetSetting> GetSettings()
    {
        // Every toggle here adds or removes a row, so each one re-lays the widget out.
        WidgetSetting Row(string label, string? desc, System.Func<bool> get,
                          System.Action<bool> set, string group = "Rows",
                          SettingTier tier = SettingTier.Basic) =>
            WidgetSetting.Toggle(label, get, v => { set(v); ApplyToggles(); },
                                 group: group, description: desc, tier: tier);

        yield return WidgetSetting.Note(
            "Fuel, litres per lap and laps left are always shown. Laps left already "
            + "allows for the fuel the engine cannot draw from the bottom of the tank.",
            group: "Rows");

        yield return Row("Tank %", null, () => ShowTankPct, v => ShowTankPct = v);
        yield return Row("Trend beside L/LAP",
            "▲ or ▼ once the last three laps drift more than 1% from the ten-lap average.",
            () => ShowTrend, v => ShowTrend = v);
        yield return Row("3-lap average", null, () => ShowL3Average, v => ShowL3Average = v);
        yield return Row("5-lap average", null, () => ShowL5Average, v => ShowL5Average = v);

        yield return Row("Pit window",
            "The lap range to pit in. Turns red and says PIT NOW when you are in it, LATE once the last safe lap has gone.",
            () => ShowPitWindow, v => ShowPitWindow = v, group: "Strategy");
        yield return Row("Fill amount",
            "Litres to add at the next stop to reach the finish with the buffer intact.",
            () => ShowFillAmount, v => ShowFillAmount = v, group: "Strategy");
        yield return Row("Predicted pit lap",
            "The lap the tank runs dry at the current average.",
            () => ShowPitLap, v => ShowPitLap = v, group: "Strategy", tier: SettingTier.Advanced);
        yield return Row("Full-tank laps",
            "How many laps a full tank covers — the stint length to plan around.",
            () => ShowTankLaps, v => ShowTankLaps = v, group: "Strategy", tier: SettingTier.Advanced);
        yield return Row("Reserve",
            "The unusable fuel at the bottom of the tank and what that leaves usable.",
            () => ShowBuffer, v => ShowBuffer = v, group: "Strategy", tier: SettingTier.Advanced);

        yield return Row("Saving section",
            "Projected surplus or deficit at the flag, the per-lap saving target, and the live saving rate.",
            () => ShowSavingSection, v => ShowSavingSection = v, group: "Saving");
        yield return WidgetSetting.Slider(
            "Manual save target", 0, 1.0,
            () => ManualSaveTarget, v => ManualSaveTarget = (float)v,
            group: "Saving",
            description: "Override the computed target with a fixed litres-per-lap saving. 0 uses the computed value.",
            tier: SettingTier.Advanced,
            step: 0.05,
            format: v => v <= 0 ? "Auto" : $"{v:F2} L/lap");

        yield return Row("Alert bar",
            "The strip along the bottom that flags low fuel and pit-now conditions.",
            () => ShowAlert, v => ShowAlert = v, group: "Alerts");

        foreach (var s in base.GetSettings()) yield return s;
    }
}
