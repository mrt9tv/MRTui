using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// Shift points decide where the orange "shift now" band sits, so these pin both
/// the source-selection order and the window geometry.
///
/// Representative GT3 numbers are used throughout: 8500 optimal, 8800 blink.
/// </summary>
public class ShiftPointServiceTests
{
    private const float First = 8000f;
    private const float Optimal = 8500f;
    private const float Last = 8750f;   // deliberately well above Optimal
    private const float Blink = 8800f;
    private const float Redline = 9000f;

    private static ShiftPointService WithGt3Profile()
    {
        var svc = new ShiftPointService();
        svc.ApplyCarProfile(Redline, First, Optimal, Last, Blink, forwardGears: 6);
        return svc;
    }

    private static TelemetryData At(float rpm, int gear = 3) => new() { RPM = rpm, Gear = gear };

    // ── Window geometry: the headline fix ─────────────────────────────

    [Fact]
    public void Window_IsNotStretchedToLastRpm()
    {
        // The previous implementation set the window end to
        // Math.Max(optimal + 0.5%, LastRPM), which pushed it to 8750 here — a
        // 590 RPM band rather than the documented ~215.
        var points = WithGt3Profile().ResolveShiftPoints(At(Optimal));

        Assert.True(points.WindowEnd < Last,
            $"window end {points.WindowEnd} must not be stretched to LastRPM {Last}");
        Assert.Equal(Optimal * 1.005f, points.WindowEnd, 0);
    }

    [Fact]
    public void Window_UsesTheConfiguredAsymmetricMargins()
    {
        var points = WithGt3Profile().ResolveShiftPoints(At(Optimal));

        Assert.Equal(Optimal * (1f - ShiftPointService.OptimalWindowBeforePct), points.WindowStart, 0);
        Assert.Equal(Optimal * (1f + ShiftPointService.OptimalWindowAfterPct), points.WindowEnd, 0);

        // Asymmetric on purpose: early costs little, late hits the limiter.
        float below = points.Optimal - points.WindowStart;
        float above = points.WindowEnd - points.Optimal;
        Assert.True(below > above);
    }

    [Fact]
    public void Window_NeverReachesIntoTheBlinkZone()
    {
        var svc = new ShiftPointService();
        // A car whose optimal sits almost at the blink threshold.
        svc.ApplyCarProfile(redline: 9000f, firstRpm: 8700f, shiftRpm: 8790f,
                            lastRpm: 8795f, blinkRpm: 8800f, forwardGears: 6);

        var points = svc.ResolveShiftPoints(At(8790f));

        Assert.True(points.WindowEnd < 8800f,
            $"window end {points.WindowEnd} must stay below blink 8800");
    }

    // ── Zone classification ───────────────────────────────────────────

    [Theory]
    [InlineData(6000f, ShiftZone.Safe)]
    [InlineData(8100f, ShiftZone.Warning)]   // between First and window start
    [InlineData(8400f, ShiftZone.Optimal)]   // inside the window (8330-8542)
    [InlineData(8500f, ShiftZone.Optimal)]   // exactly optimal
    [InlineData(8700f, ShiftZone.Danger)]    // past the window
    [InlineData(8900f, ShiftZone.Danger)]    // past blink
    public void Zone_MapsRpmToTheExpectedBand(float rpm, ShiftZone expected)
    {
        var svc = WithGt3Profile();
        var data = At(rpm);
        svc.Update(data);

        Assert.Equal(expected, data.ShiftZone);
    }

    [Theory]
    [InlineData(0)]   // neutral
    [InlineData(-1)]  // reverse
    public void Zone_NeverPromptsOutOfGear(int gear)
    {
        var svc = WithGt3Profile();
        var data = At(Optimal, gear);
        svc.Update(data);

        Assert.Equal(ShiftZone.Safe, data.ShiftZone);
    }

    [Fact]
    public void Zone_ShowsOverRevInTopGear()
    {
        // Being on the limiter is worth seeing whether or not another gear exists.
        var svc = WithGt3Profile();
        var data = At(8900f, gear: 6); // 6 of 6
        svc.Update(data);

        Assert.Equal(ShiftZone.Danger, data.ShiftZone);
    }

    // ── Source selection ──────────────────────────────────────────────

    [Fact]
    public void PrefersSessionProfileOverLiveTelemetry()
    {
        var svc = WithGt3Profile();

        // Telemetry disagrees with the profile; the profile wins.
        var data = At(Optimal);
        data.PlayerCarSLShiftRPM = 5000f;
        data.PlayerCarSLBlinkRPM = 5200f;

        svc.Update(data);

        Assert.Equal(Optimal, data.ShiftOptimalRPM, 0);
        Assert.True(data.ShiftPointsAreAuthoritative);
    }

    [Fact]
    public void FallsBackToLiveTelemetryBeforeTheProfileArrives()
    {
        var svc = new ShiftPointService(); // no profile yet

        var data = At(7000f);
        data.PlayerCarSLFirstRPM = 6500f;
        data.PlayerCarSLShiftRPM = 7200f;
        data.PlayerCarSLBlinkRPM = 7500f;

        svc.Update(data);

        Assert.Equal(7200f, data.ShiftOptimalRPM, 0);
        Assert.True(data.ShiftPointsAreAuthoritative);
    }

    [Fact]
    public void FallsBackToLearnedEstimateWhenNothingIsKnown()
    {
        var svc = new ShiftPointService();
        var data = At(7000f);

        svc.Update(data);

        // Still produces a usable answer, but flags it as not authoritative so a
        // caller can choose to stay quiet until the real values arrive.
        Assert.True(data.ShiftOptimalRPM > 0);
        Assert.False(data.ShiftPointsAreAuthoritative);
    }

    // ── Redline ───────────────────────────────────────────────────────

    [Fact]
    public void Redline_PrefersTheCarsReportedValue()
    {
        var svc = WithGt3Profile();
        var data = At(5000f);
        svc.Update(data);

        Assert.Equal(Redline, data.ShiftRedlineRPM, 0);
    }

    [Fact]
    public void Redline_NeverReturnsZero()
    {
        // A zero redline would make the RPM ring divide by zero.
        var svc = new ShiftPointService();
        var data = At(0f);
        svc.Update(data);

        Assert.True(data.ShiftRedlineRPM > 1000f);
    }

    // ── Car changes ───────────────────────────────────────────────────

    [Fact]
    public void ChangingCar_ResetsTheLearnedFallback()
    {
        // The old static learner was never reset, so a Formula car's observed
        // maximum kept scaling the ring after switching to a GT3.
        var svc = new ShiftPointService();

        // Learn a high-revving car with no profile available.
        var formula = At(15000f);
        svc.Update(formula);

        // Now a profile for a completely different car arrives.
        svc.ApplyCarProfile(redline: 7000f, firstRpm: 6000f, shiftRpm: 6500f,
                            lastRpm: 6700f, blinkRpm: 6800f, forwardGears: 6);

        var gt3 = At(3000f);
        svc.Update(gt3);

        Assert.Equal(7000f, gt3.ShiftRedlineRPM, 0);
    }

    [Fact]
    public void ApplyCarProfile_IgnoresZeroesRatherThanOverwritingGoodData()
    {
        var svc = WithGt3Profile();

        // A later session-info update before the car is fully loaded.
        svc.ApplyCarProfile(0, 0, 0, 0, 0, 0);

        var data = At(Optimal);
        svc.Update(data);

        Assert.Equal(Optimal, data.ShiftOptimalRPM, 0);
        Assert.Equal(6, svc.ForwardGears);
    }

    [Fact]
    public void HandlesCarsWhereFirstRpmSitsAboveTheWindowStart()
    {
        // Some cars report the first shift light at or above where our window opens,
        // which would leave the yellow band with nowhere to live.
        var svc = new ShiftPointService();
        svc.ApplyCarProfile(redline: 9000f, firstRpm: 8490f, shiftRpm: 8500f,
                            lastRpm: 8600f, blinkRpm: 8800f, forwardGears: 6);

        var points = svc.ResolveShiftPoints(At(8500f));

        Assert.True(points.First < points.WindowStart,
            $"first {points.First} must sit below window start {points.WindowStart}");
    }

    [Fact]
    public void Reset_ClearsTheProfile()
    {
        var svc = WithGt3Profile();
        svc.Reset();

        var data = At(Optimal);
        svc.Update(data);

        Assert.False(data.ShiftPointsAreAuthoritative);
        Assert.Equal(0, svc.ForwardGears);
    }
}
