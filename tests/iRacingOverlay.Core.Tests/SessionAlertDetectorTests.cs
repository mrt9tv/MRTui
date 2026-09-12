using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// Session alerts come from channels added by the telemetry capability audit.
/// These pin the two properties that keep them useful rather than noisy:
/// they are edge-triggered, and they stay quiet when the data is absent.
/// </summary>
public class SessionAlertDetectorTests
{
    private static TelemetryData OnTrack() => new()
    {
        IsOnTrack = true,
        // ChanQuality reads 0 before the first network sample — a good connection
        // so tests do not trip the connection alert incidentally.
        ChanQuality = 1.0f,
    };

    private static List<NearbyEvent> Run(SessionAlertDetector detector, params TelemetryData[] frames)
    {
        var output = new List<NearbyEvent>();
        long id = 1;
        foreach (var f in frames) detector.Update(f, output, ref id);
        return output;
    }

    [Fact]
    public void NoAlerts_WhenNotOnTrack()
    {
        var detector = new SessionAlertDetector();
        var data = OnTrack();
        data.IsOnTrack = false;
        data.EngineWarnings = (int)EngineWarningFlags.OilPressureWarning;

        Assert.Empty(Run(detector, data));
    }

    [Fact]
    public void NoWeatherAlert_WhenRainSystemIsOff()
    {
        // TrackWetness reads Unknown (0) in sessions without the rain system.
        // Treating that as "dry" would fire a bogus alert on every such session.
        var detector = new SessionAlertDetector();
        var data = OnTrack();
        data.TrackWetness = (int)TrackWetnessLevel.Unknown;

        Assert.Empty(Run(detector, data, data, data));
    }

    [Fact]
    public void WeatherAlert_FiresOnceWhenWetnessChanges()
    {
        var detector = new SessionAlertDetector();

        var dry = OnTrack();
        dry.TrackWetness = (int)TrackWetnessLevel.Dry;

        var wet = OnTrack();
        wet.TrackWetness = (int)TrackWetnessLevel.ModeratelyWet;

        // First frame establishes the baseline; the change fires once and then rests.
        var events = Run(detector, dry, wet, wet, wet);

        Assert.Single(events, e => e.EventType == NearbyEventType.WeatherChange);
    }

    [Fact]
    public void DeclaredWet_FiresOnTheTransitionOnly()
    {
        var detector = new SessionAlertDetector();

        var before = OnTrack();
        var after = OnTrack();
        after.WeatherDeclaredWet = true;

        var events = Run(detector, before, after, after, after);

        Assert.Single(events, e => e.EventType == NearbyEventType.DeclaredWet);
    }

    [Fact]
    public void EngineWarning_IgnoresLimitersButReportsFaults()
    {
        var detector = new SessionAlertDetector();

        // Rev limiter and pit limiter are normal operating states, not faults.
        var limiters = OnTrack();
        limiters.EngineWarnings = (int)(EngineWarningFlags.RevLimiterActive | EngineWarningFlags.PitSpeedLimiter);
        Assert.Empty(Run(detector, limiters, limiters));

        var fault = OnTrack();
        fault.EngineWarnings = (int)EngineWarningFlags.OilPressureWarning;
        var events = Run(detector, fault);

        var alert = Assert.Single(events, e => e.EventType == NearbyEventType.EngineWarning);
        Assert.Equal("OIL PRESS", alert.DisplayText);
        Assert.Equal(NearbyEventSeverity.Critical, alert.Severity);
    }

    [Fact]
    public void ConnectionAlert_StaysQuietBeforeTheFirstNetworkSample()
    {
        var detector = new SessionAlertDetector();
        var data = OnTrack();
        data.ChanQuality = 0f; // no sample yet — not the same as a bad connection

        Assert.Empty(Run(detector, data, data));
    }

    [Fact]
    public void ConnectionAlert_FiresWhenQualityDrops()
    {
        var detector = new SessionAlertDetector();

        var good = OnTrack();
        var poor = OnTrack();
        poor.ChanQuality = 0.5f;

        var events = Run(detector, good, poor, poor);

        Assert.Single(events, e => e.EventType == NearbyEventType.PoorConnection);
    }

    [Fact]
    public void FfbAlert_StaysQuietWithoutAWheelConfigured()
    {
        var detector = new SessionAlertDetector();
        var data = OnTrack();
        data.SteeringWheelMaxForceNm = 0f;
        data.SteeringWheelPctTorque = 1.0f;

        Assert.Empty(Run(detector, data, data));
    }

    [Fact]
    public void FfbAlert_RequiresSustainedClipping()
    {
        // Momentary clipping over a kerb is normal and must not alert.
        var detector = new SessionAlertDetector();
        var clipping = OnTrack();
        clipping.SteeringWheelMaxForceNm = 8f;
        clipping.SteeringWheelPctTorque = 1.0f;

        Assert.Empty(Run(detector, clipping, clipping));
    }

    [Fact]
    public void TireSetAlert_OnlyAppliesWhenTheSeriesLimitsSets()
    {
        var detector = new SessionAlertDetector();
        var data = OnTrack();
        data.DryTireSetLimit = 0; // unlimited
        data.TireSetsAvailable = 0;

        Assert.Empty(Run(detector, data, data));
    }

    [Fact]
    public void TireSetAlert_FiresWhenSetsRunLow()
    {
        var detector = new SessionAlertDetector();

        var plenty = OnTrack();
        plenty.DryTireSetLimit = 4;
        plenty.TireSetsAvailable = 2;

        var low = OnTrack();
        low.DryTireSetLimit = 4;
        low.TireSetsAvailable = 1;

        var events = Run(detector, plenty, low);

        var alert = Assert.Single(events, e => e.EventType == NearbyEventType.LowTireSets);
        Assert.Contains("1 TYRE SET", alert.DisplayText);
    }

    [Fact]
    public void Reset_ClearsEdgeState()
    {
        var detector = new SessionAlertDetector();

        var wet = OnTrack();
        wet.WeatherDeclaredWet = true;

        Assert.Single(Run(detector, OnTrack(), wet));

        detector.Reset();

        // After a reset the same transition is new information again.
        Assert.Single(Run(detector, OnTrack(), wet));
    }

    // ── The player's own car ──────────────────────────────────────────

    private static TelemetryData OnTrackWithFlags(int playerFlags, int playerIdx = 3)
    {
        var d = OnTrack();
        d.PlayerCarIdx = playerIdx;
        d.CarIdxSessionFlags = new int[64];
        d.CarIdxSessionFlags[playerIdx] = playerFlags;
        return d;
    }

    [Fact]
    public void PlayerBlackFlag_FiresOnceOnTheRisingEdge()
    {
        const int black = 0x10000;
        var detector = new SessionAlertDetector();

        var events = Run(detector,
            OnTrackWithFlags(0),
            OnTrackWithFlags(black),
            OnTrackWithFlags(black),
            OnTrackWithFlags(0),
            OnTrackWithFlags(black));

        var blacks = events.Where(e => e.EventType == NearbyEventType.BlackFlag).ToList();
        Assert.Equal(2, blacks.Count);
        Assert.All(blacks, e => Assert.Equal(-1, e.CarIdx));
        Assert.Equal(NearbyEventSeverity.Critical, blacks[0].Severity);
    }

    [Fact]
    public void PlayerFlags_DoNotSuppressEachOther()
    {
        const int black = 0x10000, repair = 0x100000;
        var detector = new SessionAlertDetector();

        var events = Run(detector, OnTrackWithFlags(0), OnTrackWithFlags(black | repair));

        Assert.Contains(events, e => e.EventType == NearbyEventType.BlackFlag);
        Assert.Contains(events, e => e.EventType == NearbyEventType.MeatballFlag);
    }

    [Fact]
    public void Incidents_SeedSilently_ThenReportEachGain()
    {
        var detector = new SessionAlertDetector();
        TelemetryData Inc(int count) { var d = OnTrack(); d.PlayerCarMyIncidentCount = count; return d; }

        var events = Run(detector, Inc(6), Inc(6), Inc(7), Inc(7), Inc(11))
            .Where(e => e.EventType == NearbyEventType.IncidentGained).ToList();

        Assert.Equal(2, events.Count);
        Assert.Equal("+1x  (7x)", events[0].DisplayText);
        Assert.Equal(NearbyEventSeverity.Info, events[0].Severity);
        Assert.Equal("+4x  (11x)", events[1].DisplayText);
        Assert.Equal(NearbyEventSeverity.Warning, events[1].Severity);
    }

    [Fact]
    public void PitLane_ReportsTransitionsOnly()
    {
        var detector = new SessionAlertDetector();
        TelemetryData Pits(bool open) { var d = OnTrack(); d.PitsOpen = open; return d; }

        var events = Run(detector, Pits(false), Pits(false), Pits(true), Pits(true), Pits(false))
            .Where(e => e.EventType == NearbyEventType.PitLaneStatus).ToList();

        Assert.Equal(2, events.Count);
        Assert.Equal("PITS OPEN", events[0].DisplayText);
        Assert.Equal("PITS CLOSED", events[1].DisplayText);
        Assert.Equal(NearbyEventSeverity.Warning, events[1].Severity);
    }
}

public class TelemetryStatusTests
{
    [Theory]
    [InlineData(TrackWetnessLevel.Dry, false)]
    [InlineData(TrackWetnessLevel.MostlyDry, false)]
    [InlineData(TrackWetnessLevel.VeryLightlyWet, false)]
    [InlineData(TrackWetnessLevel.LightlyWet, true)]
    [InlineData(TrackWetnessLevel.ExtremelyWet, true)]
    public void IsWet_TripsOnceDryTiresAreCompromised(TrackWetnessLevel level, bool expected)
    {
        Assert.Equal(expected, TelemetryStatus.IsWet((int)level));
    }

    [Fact]
    public void MostUrgentEngineWarning_PrefersTheFastestRaceEnder()
    {
        int both = (int)(EngineWarningFlags.WaterTempWarning | EngineWarningFlags.EngineStalled);
        Assert.Equal("STALLED", TelemetryStatus.MostUrgentEngineWarning(both));
    }

    [Fact]
    public void MostUrgentEngineWarning_IgnoresNormalOperatingStates()
    {
        int limiters = (int)(EngineWarningFlags.RevLimiterActive | EngineWarningFlags.PitSpeedLimiter);
        Assert.Null(TelemetryStatus.MostUrgentEngineWarning(limiters));
    }

    [Theory]
    [InlineData(0.5f, false)]
    [InlineData(0.98f, false)]
    [InlineData(1.0f, true)]
    [InlineData(1.2f, true)]
    public void IsFfbClipping_TripsAtSaturation(float pctTorque, bool expected)
    {
        Assert.Equal(expected, TelemetryStatus.IsFfbClipping(pctTorque));
    }

    [Fact]
    public void WetnessLabel_IsEmptyForUnknown()
    {
        Assert.Equal("", TelemetryStatus.WetnessLabel((int)TrackWetnessLevel.Unknown));
        Assert.Equal("DRY", TelemetryStatus.WetnessLabel((int)TrackWetnessLevel.Dry));
    }
}
