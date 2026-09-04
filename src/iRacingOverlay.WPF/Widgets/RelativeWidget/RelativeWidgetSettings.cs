using System.Collections.Generic;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.RelativeWidget;

public partial class RelativeWidget
{
    /// <inheritdoc/>
    public override IEnumerable<WidgetSetting> GetSettings()
    {
        yield return WidgetSetting.Toggle(
            "Smart row count", () => UseSmartRowCount, v => { UseSmartRowCount = v; RecalcHeight(); },
            group: "Rows",
            description: "Adjust the split of cars ahead and behind based on your position — all behind when leading.");

        yield return WidgetSetting.Slider(
            "Cars ahead", 1, 12,
            () => MaxAhead, v => { MaxAhead = (int)v; RecalcHeight(); },
            group: "Rows");

        yield return WidgetSetting.Slider(
            "Cars behind", 1, 12,
            () => MaxBehind, v => { MaxBehind = (int)v; RecalcHeight(); },
            group: "Rows");

        yield return WidgetSetting.Choice(
            "Driver name format",
            new[]
            {
                "J.Smith", "Full name", "Initials (J.S.)", "First three (JOE)",
                "Last name", "First name", "3-letter code (SMI)", "Smith, J.",
                "J.Smit", "Joe S.", "SMITH", "J.Smi", "Smith J", "JSmith",
            },
            () => (int)DriverNameFormat,
            v => { DriverNameFormat = (NameFormat)v; RecalcLayout(); },
            group: "Rows");

        // ── Columns ───────────────────────────────────────────────────
        WidgetSetting Column(string label, string? desc, System.Func<bool> get,
                             System.Action<bool> set, SettingTier tier = SettingTier.Basic) =>
            WidgetSetting.Toggle(label,
                get,
                v => { set(v); RecalcLayout(); RecalcHeight(); },
                group: "Columns", description: desc, tier: tier);

        yield return Column("Car number", null, () => ShowCarNumber, v => ShowCarNumber = v);
        yield return Column("Gap", "Gap to the car directly ahead on the road.", () => ShowInterval, v => ShowInterval = v);
        yield return Column("Last lap", null, () => ShowLastLap, v => ShowLastLap = v);
        yield return Column("Driver info", "License badge and iRating.", () => ShowDriverInfo, v => ShowDriverInfo = v);
        yield return Column("Info bar", "Estimated laps, time remaining and incidents along the bottom.", () => ShowInfoBar, v => ShowInfoBar = v);
        yield return Column("Alternating rows", "Shade every other row.", () => ShowAlternateRowShading, v => ShowAlternateRowShading = v);

        yield return Column("Class position", null, () => ShowClassPosition, v => ShowClassPosition = v, SettingTier.Advanced);
        yield return Column("Full iRating", "2035 rather than 2.0k.", () => UseFullIRating, v => UseFullIRating = v, SettingTier.Advanced);
        yield return Column("Car model", "Three-letter car abbreviation.", () => ShowCarModel, v => ShowCarModel = v, SettingTier.Advanced);
        yield return Column("Closing rate", "Arrow showing whether the gap is shrinking.", () => ShowClosingRate, v => ShowClosingRate = v, SettingTier.Advanced);
        yield return Column("Pit stops", null, () => ShowPitStopCount, v => ShowPitStopCount = v, SettingTier.Advanced);
        yield return Column("Position change", "Places gained or lost since the start.", () => ShowPositionChange, v => ShowPositionChange = v, SettingTier.Advanced);
        yield return Column("Nationality", null, () => ShowNationality, v => ShowNationality = v, SettingTier.Advanced);
        yield return Column("Class legend", null, () => ShowClassLegend, v => ShowClassLegend = v, SettingTier.Advanced);

        yield return WidgetSetting.Toggle("Colour the gap column",
            () => ShowSectorDelta, v => { ShowSectorDelta = v; RecalcLayout(); },
            group: "Emphasis", description: "Green when pulling away, red when being caught.", tier: SettingTier.Advanced);
        yield return WidgetSetting.Toggle("Dim lapped cars",
            () => ShowLappedDim, v => { ShowLappedDim = v; RecalcLayout(); },
            group: "Emphasis", tier: SettingTier.Advanced);
        yield return WidgetSetting.Toggle("Danger glow",
            () => ShowDangerGlow, v => { ShowDangerGlow = v; RecalcLayout(); },
            group: "Emphasis", description: "Red tint on a car closing fast.", tier: SettingTier.Advanced);
        yield return WidgetSetting.Toggle("Animate rows",
            () => EnableRowAnimation, v => { EnableRowAnimation = v; RecalcLayout(); },
            group: "Emphasis", description: "Slide rows into place as positions change.", tier: SettingTier.Advanced);

        foreach (var s in base.GetSettings()) yield return s;
    }

    protected override bool SupportsResize => false;
}
