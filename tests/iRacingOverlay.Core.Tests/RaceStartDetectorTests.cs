using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// Drives the start detector through the light sequence one 60 Hz frame at a
/// time. Each scenario is the frames a real start produces — lights, baseline,
/// then whichever input the technique uses — so the numbers below are what a
/// driver would see.
/// </summary>
public class RaceStartDetectorTests
{
    private const uint Green = 0x4;
    private const uint Ready = 0x20000000;
    private const uint Set = 0x40000000;
    private const uint Go = 0x80000000;
    private const int Parade = 3;
    private const int Racing = 4;
    private const double Tick = 1.0 / 60.0;

    /// <summary>A car on the grid with an adjustable clock, pedals and gear.</summary>
    private sealed class Grid
    {
        public readonly RaceStartDetector Detector = new();
        public double Time = 100.0;
        public uint Flags;
        public int State = Racing;
        public float Throttle, Clutch = 1f, Speed;
        public int Gear = 1;

        public void Frame()
        {
            Detector.Update(new TelemetryData
            {
                SessionNum = 1,
                SessionTime = Time,
                SessionFlags = Flags,
                SessionState = State,
                ThrottleRaw = Throttle,
                ClutchRaw = Clutch,
                Speed = Speed,
                Gear = Gear,
                IsOnTrack = true,
            });
            Time += Tick;
        }

        public void Frames(int n) { for (int i = 0; i < n; i++) Frame(); }

        /// <summary>Ready, Set, then Go on the last frame; leaves the clock at lights-out.</summary>
        public void Lights()
        {
            Flags = Ready; Frames(30);
            Flags = Set; Frames(30);
            Flags = Go; Frame();
        }
    }

    private static RaceStartResult Measured(Grid g)
    {
        Assert.True(g.Detector.JustMeasured, "expected a result on this frame");
        return g.Detector.Result!;
    }

    // ── Techniques ────────────────────────────────────────────────────

    [Fact]
    public void ClutchStart_TimesTheClutchRelease_NotTheThrottleAlreadyHeld()
    {
        var g = new Grid { Clutch = 0f, Throttle = 0.6f, Gear = 1 }; // revs held, clutch down
        g.Lights();

        g.Frames(12);                       // 0.2 s of nothing
        g.Clutch = 0.5f; g.Frame();         // release begins

        Assert.False(g.Detector.JustMeasured, "reaction alone is not the result — the launch is still pending");

        g.Frames(20);
        g.Speed = 2f; g.Frame();            // car moves

        var r = Measured(g);
        Assert.Equal(LaunchTechnique.Clutch, r.Technique);
        Assert.Equal(LaunchInput.Clutch, r.FirstInput);
        Assert.InRange(r.ReactionSeconds, 0.19f, 0.23f);
        Assert.InRange(r.LaunchSeconds, 0.54f, 0.58f);
        Assert.False(r.JumpStart);
    }

    [Fact]
    public void NeutralStart_TimesTheShiftIntoGear()
    {
        var g = new Grid { Gear = 0, Clutch = 1f, Throttle = 0f };
        g.Lights();

        g.Frames(18);                       // 0.3 s
        g.Gear = 1; g.Frame();
        g.Throttle = 0.8f; g.Speed = 1f; g.Frame();

        var r = Measured(g);
        Assert.Equal(LaunchTechnique.NeutralToGear, r.Technique);
        Assert.Equal(LaunchInput.Gear, r.FirstInput);
        Assert.Equal(0, r.GearAtGo);
        Assert.InRange(r.ReactionSeconds, 0.29f, 0.33f);
    }

    [Fact]
    public void ThrottleOnlyStart_TimesTheThrottle()
    {
        var g = new Grid { Gear = 1, Clutch = 1f, Throttle = 0.05f }; // auto-clutch car, idling in first
        g.Lights();

        g.Frames(9);                        // 0.15 s
        g.Throttle = 0.9f; g.Frame();
        g.Speed = 1f; g.Frame();

        var r = Measured(g);
        Assert.Equal(LaunchTechnique.ThrottleOnly, r.Technique);
        Assert.Equal(LaunchInput.Throttle, r.FirstInput);
        Assert.InRange(r.ReactionSeconds, 0.14f, 0.18f);
    }

    [Fact]
    public void ThrottleJitter_UnderTheThreshold_IsNotAnInput()
    {
        var g = new Grid { Clutch = 0f, Throttle = 0.6f };
        g.Lights();

        g.Throttle = 0.68f; g.Frames(30);   // modulating the revs, +8%
        Assert.Null(g.Detector.Result);
    }

    // ── Jump start ────────────────────────────────────────────────────

    [Fact]
    public void MovingUnderTheLights_IsAJumpStart()
    {
        var g = new Grid { Clutch = 0f, Throttle = 0.6f };
        g.Flags = Ready; g.Frames(30);
        g.Flags = Set; g.Frames(10);
        g.Speed = 1.5f; g.Frame();          // rolled forward before Go

        var r = Measured(g);
        Assert.True(r.JumpStart);
        Assert.Equal(0f, r.ReactionSeconds);

        // Go arrives later; nothing more is measured for this start.
        g.Flags = Go; g.Frames(60);
        Assert.False(g.Detector.JustMeasured);
        Assert.True(g.Detector.Result!.JumpStart);
    }

    // ── Rolling ───────────────────────────────────────────────────────

    [Fact]
    public void RollingStart_MeasuresThrottleIncreaseFromTheGreen()
    {
        var g = new Grid { State = Parade, Speed = 20f, Throttle = 0.3f, Gear = 3 };
        g.Frames(60);                                       // pace lap
        g.State = Racing; g.Flags = Green; g.Frame();       // green

        g.Frames(24);                                       // 0.4 s
        g.Throttle = 1f; g.Frame();

        var r = Measured(g);
        Assert.Equal(LaunchTechnique.Rolling, r.Technique);
        Assert.InRange(r.ReactionSeconds, 0.39f, 0.43f);
        Assert.Equal(0f, r.LaunchSeconds);                  // already moving — no launch time
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    [Fact]
    public void Timeout_WithNoInput_ReportsNothing()
    {
        var g = new Grid { Clutch = 0f, Throttle = 0.6f };
        g.Lights();
        g.Frames(6 * 60);                   // six seconds of nothing — stalled, or away
        Assert.Null(g.Detector.Result);
    }

    [Fact]
    public void Restart_ReArmsAndKeepsTheBest()
    {
        var g = new Grid { Gear = 1, Clutch = 1f, Throttle = 0f };
        g.Lights();
        g.Frames(12); g.Throttle = 1f; g.Frame(); g.Speed = 1f; g.Frame();
        var first = Measured(g);
        Assert.InRange(first.SessionBestSeconds, 0.19f, 0.23f);

        // Red flag, back to the grid, lights again — a slower second start.
        g.Flags = 0; g.State = 2; g.Speed = 0f; g.Throttle = 0f; g.Frames(120);
        g.State = Racing;
        g.Lights();
        g.Frames(30); g.Throttle = 1f; g.Frame(); g.Speed = 1f; g.Frame();

        var second = Measured(g);
        Assert.InRange(second.ReactionSeconds, 0.49f, 0.53f);
        Assert.Equal(first.ReactionSeconds, second.SessionBestSeconds);
    }

    [Fact]
    public void AbortedLights_DoNotLeaveTheDetectorArmed()
    {
        var g = new Grid();
        g.Flags = Ready; g.Frames(10);
        g.Flags = 0; g.Frame();             // lights withdrawn, still racing state, no parade

        g.Throttle = 1f; g.Speed = 5f; g.Frames(30);
        Assert.Null(g.Detector.Result);
    }

    // ── Rolling, already flat ─────────────────────────────────────────

    [Fact]
    public void RollingStart_FlatBeforeTheGreen_ReportsNothingToMeasure()
    {
        var g = new Grid { State = Parade, Speed = 30f, Throttle = 1f, Gear = 4 };
        g.Frames(60);
        g.State = Racing; g.Flags = Green; g.Frame();

        var r = Measured(g);
        Assert.True(r.FlatAtGreen);
        Assert.Equal(LaunchTechnique.Rolling, r.Technique);
        Assert.Equal(0f, r.ReactionSeconds);
    }

    // ── Sequence ──────────────────────────────────────────────────────

    [Fact]
    public void Sequence_IncrementsPerStart()
    {
        var g = new Grid { Gear = 1, Clutch = 1f, Throttle = 0f };
        g.Lights();
        g.Frames(12); g.Throttle = 1f; g.Frame(); g.Speed = 1f; g.Frame();
        Assert.Equal(1, Measured(g).Sequence);

        g.Flags = 0; g.State = 2; g.Speed = 0f; g.Throttle = 0f; g.Frames(120);
        g.State = Racing;
        g.Lights();
        g.Frames(12); g.Throttle = 1f; g.Frame(); g.Speed = 1f; g.Frame();
        Assert.Equal(2, Measured(g).Sequence);
    }
}
