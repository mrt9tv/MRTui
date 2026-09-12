using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// Drives the fuel calculator through synthetic laps. Each lap is two frames —
/// one just after the line and one just before it — which is enough for the
/// lap-change detection and keeps the burn per lap exact.
/// </summary>
public class FuelCalculatorServiceTests
{
    private const float Tank = 100f;
    private const float LapTime = 90f;

    private sealed class Sim
    {
        public readonly FuelCalculatorService Service = new();
        public float Fuel = Tank;
        public int Lap;

        private TelemetryData Frame(float distPct) => new()
        {
            FuelLevel = Fuel,
            FuelLevelMax = Tank,
            Lap = Lap,
            LapDistPct = distPct,
            LapLastLapTime = LapTime,
            LapBestLapTime = LapTime,
            SessionNum = 0,
            SessionLapsRemain = 32767,   // iRacing's "not lap-limited" sentinel
            SessionLapsTotal = 32767,
            SessionTimeRemain = 3600f,
            TrackName = "Test",
            CarScreenName = "Car",
        };

        /// <summary>Complete one lap burning <paramref name="litres"/>.</summary>
        public void RunLap(float litres)
        {
            Service.Update(Frame(0.05f));
            Fuel -= litres;
            Service.Update(Frame(0.95f));
            Lap++;
            Service.Update(Frame(0.02f));
        }

        public FuelData Data => Service.CurrentData;
    }

    // The first completed lap is always discarded as partial, so a sim needs
    // one throwaway lap before the numbers below start counting.
    private static Sim Warm()
    {
        var sim = new Sim();
        sim.RunLap(3f);
        return sim;
    }

    [Fact]
    public void Trend_IsZeroUntilSixCleanLaps()
    {
        var sim = Warm();
        for (int i = 0; i < 5; i++) sim.RunLap(3f);

        Assert.Equal(5, sim.Data.LapsCompleted);
        Assert.Equal(0f, sim.Data.ConsumptionTrendPct);
    }

    [Fact]
    public void Trend_RisesWhenRecentLapsBurnMore()
    {
        var sim = Warm();
        for (int i = 0; i < 7; i++) sim.RunLap(3.0f);
        for (int i = 0; i < 3; i++) sim.RunLap(3.3f);

        // L3 = 3.3, L10 = (7×3.0 + 3×3.3)/10 = 3.09 → +6.8%
        Assert.InRange(sim.Data.ConsumptionTrendPct, 0.06f, 0.08f);
    }

    [Fact]
    public void Trend_FallsWhenSaving()
    {
        var sim = Warm();
        for (int i = 0; i < 7; i++) sim.RunLap(3.0f);
        for (int i = 0; i < 3; i++) sim.RunLap(2.7f);

        Assert.InRange(sim.Data.ConsumptionTrendPct, -0.08f, -0.06f);
    }

    [Fact]
    public void FullTankLaps_UsesCapacityLessReserve()
    {
        var sim = Warm();
        for (int i = 0; i < 3; i++) sim.RunLap(4f);

        // (100 − 0.3 sputter) / 4 = 24.9
        Assert.InRange(sim.Data.FullTankLaps, 24.8f, 25.0f);
    }

    [Fact]
    public void PitWindow_EndsWhereFuelRunsOut()
    {
        var sim = Warm();
        for (int i = 0; i < 3; i++) sim.RunLap(10f);

        // 67 L left (3 on the warm-up, 30 since) at 10 L/lap → 6.67 usable laps →
        // the window ends six laps out, on lap 10.
        Assert.Equal(sim.Lap + 6, sim.Data.PitWindowEnd);
        Assert.Equal(sim.Data.PitWindowEnd - 2, sim.Data.PitWindowStart);
        Assert.False(sim.Data.CanFinishWithoutStop);
    }

    [Fact]
    public void Reset_ClearsDerivedNumbers()
    {
        var sim = Warm();
        for (int i = 0; i < 10; i++) sim.RunLap(3f);
        Assert.True(sim.Data.FullTankLaps > 0);

        sim.Service.Reset();

        Assert.Equal(0f, sim.Data.FullTankLaps);
        Assert.Equal(0f, sim.Data.ConsumptionTrendPct);
        Assert.Equal(0, sim.Data.LapsCompleted);
    }
}
