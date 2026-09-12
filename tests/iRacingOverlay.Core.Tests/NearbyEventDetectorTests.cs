using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// Session-level behaviour of the feed detector — the paths that do not need a
/// nearby car. These pin three defects found auditing why the feed's switches
/// and start events did not behave.
/// </summary>
public class NearbyEventDetectorTests
{
    private const int Parade = 3;
    private const int Racing = 4;
    private const int Checkered = 5;

    private static TelemetryData Session(int state, uint flags = 0) => new()
    {
        SessionState = state,
        SessionFlags = flags,
        IsOnTrack = true,
        ChanQuality = 1f,
    };

    private static IEnumerable<string> Texts(NearbyEventDetector d) =>
        d.ActiveEvents.Select(e => e.DisplayText);

    [Fact]
    public void GoFollowsPaceLaps_DespiteSharingAnEventType()
    {
        // "PACE LAPS" and "GO GO GO!" are both StartSequence. The pace event is
        // cleared (fading out) on the same tick the go is emitted; the dedup used
        // to count that fade as "already showing" and drop the green entirely.
        var d = new NearbyEventDetector();
        d.Update(Session(Parade), null);
        Assert.Contains(Texts(d), t => t.Contains("PACE LAPS"));

        d.Update(Session(Racing), null);
        Assert.Contains(Texts(d), t => t.Contains("GO GO GO"));
    }

    [Fact]
    public void Flags_AppearWithNoOtherCarOnTrack()
    {
        // Session-level detection sat behind the relative-table guard, so in an
        // empty session no flag was ever shown.
        var d = new NearbyEventDetector();
        d.Update(Session(Racing), relativeEntries: null);
        d.Update(Session(Checkered), relativeEntries: null);

        Assert.Contains(Texts(d), t => t.Contains("CHECKERED"));
    }

    [Fact]
    public void DisabledType_IsRemovedAtEmission_NotJustHidden()
    {
        var d = new NearbyEventDetector();
        d.DisabledTypes.Add(NearbyEventType.StartSequence);

        d.Update(Session(Parade), null);
        d.Update(Session(Racing), null);

        Assert.DoesNotContain(d.ActiveEvents, e => e.EventType == NearbyEventType.StartSequence);
    }

    [Fact]
    public void EnablingAType_LetsTheNextEventThrough()
    {
        var d = new NearbyEventDetector();
        d.DisabledTypes.Add(NearbyEventType.CheckeredFlag);
        d.Update(Session(Racing), null);
        d.Update(Session(Checkered), null);
        Assert.DoesNotContain(d.ActiveEvents, e => e.EventType == NearbyEventType.CheckeredFlag);

        // Switch it back on: a fresh chequered is announced, the old one is not replayed.
        d.DisabledTypes.Clear();
        d.Update(Session(Racing), null);
        d.Update(Session(Checkered), null);
        Assert.Contains(d.ActiveEvents, e => e.EventType == NearbyEventType.CheckeredFlag);
    }
}
