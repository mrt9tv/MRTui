using System.Collections.Generic;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.TurnDisplayWidget;

public partial class TurnDisplayWidget
{
    /// <inheritdoc/>
    public override IEnumerable<WidgetSetting> GetSettings()
    {
        yield return WidgetSetting.Note(
            "Names the corner you are in and the one coming up, from the track's "
            + "turn database. Tracks without an entry show turn numbers only.",
            group: "Turns");

        yield return WidgetSetting.Toggle(
            "Turn names", () => ShowTurnName, v => ShowTurnName = v,
            group: "Turns",
            description: "Off narrows the widget to just the turn numbers.");

        yield return WidgetSetting.Toggle(
            "Colour the border through the corner", () => AnimateBorder, v => AnimateBorder = v,
            group: "Turns",
            description: "Shifts the border from entry to exit colour as you progress through the turn.",
            tier: SettingTier.Advanced);

        foreach (var s in base.GetSettings()) yield return s;
    }

    protected override bool SupportsResize => false;
}
