using System.Collections.Generic;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.PitConfirmWidget;

public partial class PitConfirmWidget
{
    /// <inheritdoc/>
    public override IEnumerable<WidgetSetting> GetSettings()
    {
        yield return WidgetSetting.Note(
            "Appears as you approach the pit box and shows what service is armed. "
            + "Warns when nothing is selected, the pits are closed, or the fuel armed "
            + "is short of what the fuel calculator says you need.",
            group: "Pit service");

        yield return WidgetSetting.Toggle(
            "Also show in the garage", () => ShowInGarage, v => ShowInGarage = v,
            group: "Pit service",
            description: "Check service before you leave the box, not just on the way in.");

        foreach (var s in base.GetSettings()) yield return s;
    }

    protected override bool SupportsResize => false;
}

