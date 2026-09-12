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
            description: "How many events can be shown at once. The most severe win when there are more.");

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

        // ── Event families ────────────────────────────────────────────
        // One switch per family, and every event type belongs to a family. A
        // switch takes effect in the detector, so a disabled family never
        // occupies one of the six slots.
        WidgetSetting Family(string label, string desc, System.Func<bool> get, System.Action<bool> set,
                             SettingTier tier = SettingTier.Basic) =>
            WidgetSetting.Toggle(label, get, v => { set(v); ApplyEventFilters(); },
                group: "Event types", description: desc, tier: tier);

        yield return Family("Cars in trouble",
            "Off-track, spins, stopped and slow cars, contact, tows.",
            () => ShowCarIncidents, v => ShowCarIncidents = v);

        yield return Family("Overtaking",
            "A faster class about to come past, or a fast car closing while you are slow.",
            () => ShowOvertakingAlert, v => ShowOvertakingAlert = v);

        yield return Family("Pit activity",
            "Cars entering, in and leaving the pits near you.",
            () => ShowPitActivity, v => ShowPitActivity = v);

        yield return Family("Flags on other cars",
            "Meatball, black flag, disqualification and local yellows.",
            () => ShowCarFlags, v => ShowCarFlags = v);

        yield return Family("Start lights",
            "Pace laps and the green.",
            () => ShowStartSequence, v => ShowStartSequence = v);

        yield return Family("Pace and safety car",
            "Safety car, end of line, free pass and wave-around.",
            () => ShowPaceFlags, v => ShowPaceFlags = v);

        yield return Family("Red, white and blue flags",
            "Red flag, final lap, and your own blue flag.",
            () => ShowRaceControlFlags, v => ShowRaceControlFlags = v);

        yield return Family("Chequered flag", "",
            () => ShowCheckeredFlag, v => ShowCheckeredFlag = v);

        yield return Family("Conditions and car warnings",
            "Weather, engine faults, connection quality, FFB clipping, tyre sets, pit lane open or closed.",
            () => ShowConditions, v => ShowConditions = v);

        yield return Family("My incidents",
            "Each time your own incident count goes up, with the new total.",
            () => ShowMyIncidents, v => ShowMyIncidents = v);

        foreach (var s in base.GetSettings()) yield return s;
    }

    /// <summary>The feed sizes itself from its row count, so no single size slider.</summary>
    protected override bool SupportsResize => false;
}
