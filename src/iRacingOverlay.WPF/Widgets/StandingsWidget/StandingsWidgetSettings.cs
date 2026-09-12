using System.Collections.Generic;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.StandingsWidget;

public partial class StandingsWidget
{
    /// <inheritdoc/>
    public override IEnumerable<WidgetSetting> GetSettings()
    {
        yield return WidgetSetting.Slider(
            "Rows", 5, MAX_DISPLAY_ROWS,
            () => MaxVisibleRows, v => { MaxVisibleRows = (int)v; RecalcHeight(); },
            group: "Rows",
            description: "Most cars to list. The table shrinks to the field when fewer are running.");

        yield return WidgetSetting.Toggle(
            "Always include me", () => AlwaysShowPlayer, v => AlwaysShowPlayer = v,
            group: "Rows",
            description: "Keep your own row on screen even when you are outside the listed positions.");

        yield return WidgetSetting.Choice(
            "Driver name format",
            new[] { "J.Smith", "Full name", "Initials (J.S.)", "Last name", "3-letter code (SMI)", "J.Smit" },
            () => (int)DriverNameFormat,
            v => DriverNameFormat = (NameFormat)v,
            group: "Rows");

        // ── Columns ───────────────────────────────────────────────────
        // The layout is recomputed from the toggles, and rows re-read it each frame.
        WidgetSetting Column(string label, string? desc, System.Func<bool> get,
                             System.Action<bool> set, SettingTier tier = SettingTier.Basic) =>
            WidgetSetting.Toggle(label, get,
                v => { set(v); RecalcLayout(); RecalcHeight(); },
                group: "Columns", description: desc, tier: tier);

        yield return Column("Class position", "Position within your class, beside the overall.", () => ShowClassPosition, v => ShowClassPosition = v);
        yield return Column("Car number", null, () => ShowCarNumber, v => ShowCarNumber = v);
        yield return Column("Positions gained", "Places gained or lost since the start.", () => ShowPositionChange, v => ShowPositionChange = v);
        yield return Column("Interval", "Gap to the car one place ahead.", () => ShowInterval, v => ShowInterval = v);
        yield return Column("Gap to leader", null, () => ShowGapToLeader, v => ShowGapToLeader = v);
        yield return Column("Last lap", null, () => ShowLastLap, v => ShowLastLap = v);
        yield return Column("Best lap", null, () => ShowBestLap, v => ShowBestLap = v);
        yield return Column("Current lap", "Lap each car is on — useful in timed races.", () => ShowCurrentLap, v => ShowCurrentLap = v, SettingTier.Advanced);
        yield return Column("Pit stops", null, () => ShowPitStopCount, v => ShowPitStopCount = v, SettingTier.Advanced);
        yield return Column("iRating", null, () => ShowIRating, v => ShowIRating = v, SettingTier.Advanced);
        yield return Column("License", null, () => ShowLicense, v => ShowLicense = v, SettingTier.Advanced);
        yield return Column("Car", "Car model, for multi-class fields.", () => ShowCarModel, v => ShowCarModel = v, SettingTier.Advanced);
        yield return Column("Nationality", null, () => ShowNationality, v => ShowNationality = v, SettingTier.Advanced);

        // ── Appearance ───────────────────────────────────────────────
        yield return WidgetSetting.Toggle(
            "Highlight my row", () => HighlightPlayer, v => HighlightPlayer = v,
            group: "Appearance");
        yield return WidgetSetting.Toggle(
            "Dim lapped cars", () => DimLappedCars, v => DimLappedCars = v,
            group: "Appearance",
            description: "Fade cars a lap or more down so the ones you are racing stand out.");
        yield return WidgetSetting.Toggle(
            "Alternate row shading", () => ShowAlternateRowShading, v => ShowAlternateRowShading = v,
            group: "Appearance", tier: SettingTier.Advanced);

        foreach (var s in base.GetSettings()) yield return s;
    }

    protected override bool SupportsResize => false;
}
