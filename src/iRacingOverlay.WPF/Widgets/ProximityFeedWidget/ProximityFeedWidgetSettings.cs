using System.Collections.Generic;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.ProximityFeedWidget;

public partial class ProximityFeedWidget
{
    /// <inheritdoc/>
    public override IEnumerable<WidgetSetting> GetSettings()
    {
        yield return WidgetSetting.Slider(
            "Look ahead", 1, 30,
            () => DetectionAheadSeconds, v => DetectionAheadSeconds = (float)v,
            group: "Detection range",
            description: "How far up the road to watch for incidents, in seconds.",
            format: v => $"{(int)v}s");

        yield return WidgetSetting.Slider(
            "Look behind", 1, 15,
            () => DetectionBehindSeconds, v => DetectionBehindSeconds = (float)v,
            group: "Detection range",
            description: "How far back to watch, in seconds.",
            format: v => $"{(int)v}s");

        yield return WidgetSetting.Slider(
            "Maximum events", 1, 6,
            () => MaxVisibleEvents, v => MaxVisibleEvents = (int)v,
            group: "Detection range",
            description: "How many events can be shown at once.");

        yield return WidgetSetting.Toggle(
            "Direction arrows", () => ShowDirection, v => ShowDirection = v,
            group: "Row contents",
            description: "Show whether the event is ahead of or behind you.");

        yield return WidgetSetting.Toggle(
            "Time interval", () => ShowInterval, v => ShowInterval = v,
            group: "Row contents",
            description: "Show how far away the car is, in seconds.");

        yield return WidgetSetting.Toggle(
            "Grow upward", () => GrowUpward, v => GrowUpward = v,
            group: "Row contents",
            description: "Newest event appears at the bottom instead of the top.",
            tier: SettingTier.Advanced);

        yield return WidgetSetting.Toggle(
            "Overtaking alerts", () => ShowOvertakingAlert, v => ShowOvertakingAlert = v,
            group: "Event types",
            description: "Warn when a faster-class car is about to come past.");

        yield return WidgetSetting.Toggle(
            "Race start sequence", () => ShowStartSequence, v => ShowStartSequence = v,
            group: "Event types",
            description: "Ready / Set / Go at the start.");

        yield return WidgetSetting.Toggle(
            "Chequered flag", () => ShowCheckeredFlag, v => ShowCheckeredFlag = v,
            group: "Event types");

        yield return WidgetSetting.Toggle(
            "Pace and safety car", () => ShowPaceFlags, v => ShowPaceFlags = v,
            group: "Event types",
            description: "Safety car, end of line, free pass and wave-around.");

        yield return WidgetSetting.Toggle(
            "My incidents", () => ShowMyIncidents, v => ShowMyIncidents = v,
            group: "Event types",
            description: "Note each time your own incident count goes up, with the new total.");

        foreach (var s in base.GetSettings()) yield return s;
    }

    /// <summary>The feed sizes itself from its row count, so no single size slider.</summary>
    protected override bool SupportsResize => false;
}

