using System.Collections.Generic;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.RaceStartWidget;

public partial class RaceStartWidget
{
    /// <inheritdoc/>
    public override IEnumerable<WidgetSetting> GetSettings()
    {
        yield return WidgetSetting.Note(
            "Hidden until the start lights come up. Shows the lights, then your reaction "
            + "time from lights-out to first input, how long until the car moved, and the "
            + "technique it saw — clutch release, neutral to gear, throttle, or rolling. "
            + "Times are from the sim clock at 60 Hz, so about ±16 ms.",
            group: "Race start");

        yield return WidgetSetting.Slider(
            "Keep the result on screen", 0, 300,
            () => HoldSeconds, v => HoldSeconds = (int)v,
            group: "Race start",
            description: "Seconds after the start. 0 keeps it until the session changes.",
            step: 5,
            format: v => v <= 0 ? "Whole session" : $"{(int)v} s");

        yield return WidgetSetting.Toggle(
            "Start lights", () => ShowLights, v => ShowLights = v,
            group: "Race start",
            description: "Five squares that fill red on SET and go green on GO. Off shows only the result.");

        yield return WidgetSetting.Toggle(
            "Launch time", () => ShowLaunch, v => ShowLaunch = v,
            group: "Details",
            description: "Lights-out to the car first moving.");

        yield return WidgetSetting.Toggle(
            "Technique", () => ShowTechnique, v => ShowTechnique = v,
            group: "Details");

        yield return WidgetSetting.Toggle(
            "Best reaction", () => ShowBest, v => ShowBest = v,
            group: "Details",
            description: "Your best since MRT UI was started, across restarts and sessions.");

        foreach (var s in base.GetSettings()) yield return s;
    }

    protected override bool SupportsResize => false;
}
