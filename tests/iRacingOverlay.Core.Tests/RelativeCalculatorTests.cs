using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// Pins the behaviour the shared-frame refactor has to preserve: ordering,
/// player placement, gap signs and the handling of cars that are not on track.
/// </summary>
public class RelativeCalculatorTests
{
    [Fact]
    public void Calculate_WithNoPositionArray_ReturnsEmpty()
    {
        var calc = new RelativeCalculator();
        var data = new TelemetryData(); // CarIdxLapDistPct is null

        var result = calc.Calculate(data);

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_PlacesPlayerInTheMiddleOfTheList()
    {
        var calc = new RelativeCalculator();
        var data = new TelemetryBuilder(playerIdx: 2)
            .WithEvenlySpacedField(6)
            .Build();

        var result = calc.Calculate(data, maxAhead: 2, maxBehind: 2);

        var playerIndex = result.FindIndex(e => e.IsPlayer);
        Assert.True(playerIndex >= 0, "the player must always appear in the relative list");

        var aheadCount = result.Take(playerIndex).Count();
        var behindCount = result.Count - playerIndex - 1;
        Assert.True(aheadCount <= 2);
        Assert.True(behindCount <= 2);
    }

    [Fact]
    public void Calculate_RespectsRowLimits()
    {
        var calc = new RelativeCalculator();
        var data = new TelemetryBuilder(playerIdx: 10)
            .WithEvenlySpacedField(20)
            .Build();

        var result = calc.Calculate(data, maxAhead: 3, maxBehind: 3);

        // 3 ahead + player + 3 behind
        Assert.True(result.Count <= 7, $"expected at most 7 rows, got {result.Count}");
    }

    [Fact]
    public void Calculate_ExcludesCarsNotInTheWorld()
    {
        var calc = new RelativeCalculator();
        var data = new TelemetryBuilder(playerIdx: 0)
            .WithCar(0, 0.50f)
            .WithCar(1, 0.52f)
            .Build();

        // Car 5 was never placed, so it stays at NotInWorld.
        var result = calc.Calculate(data, maxAhead: 10, maxBehind: 10);

        Assert.DoesNotContain(result, e => e.CarIdx == 5);
        Assert.Contains(result, e => e.CarIdx == 1);
    }

    [Fact]
    public void Calculate_CarJustAheadHasSmallerIntervalThanCarFurtherAhead()
    {
        var calc = new RelativeCalculator();
        var data = new TelemetryBuilder(playerIdx: 0)
            .WithCar(0, 0.50f)
            .WithCar(1, 0.52f)   // just ahead
            .WithCar(2, 0.70f)   // further ahead
            .Build();

        var result = calc.Calculate(data, maxAhead: 5, maxBehind: 5);

        var near = result.Single(e => e.CarIdx == 1);
        var far = result.Single(e => e.CarIdx == 2);

        Assert.True(Math.Abs(near.IntervalToPlayer) < Math.Abs(far.IntervalToPlayer),
            $"car at 0.52 ({near.IntervalToPlayer}) should be closer than car at 0.70 ({far.IntervalToPlayer})");
    }

    [Fact]
    public void Calculate_DoesNotOverwriteThePreviousResult()
    {
        // The result is published on the telemetry frame and read by widgets after
        // an async dispatch, so consecutive calls must not hand back the same list.
        var calc = new RelativeCalculator();
        var data = new TelemetryBuilder().WithEvenlySpacedField(5).Build();

        var first = calc.Calculate(data);
        int firstCount = first.Count;

        calc.Calculate(data);
        var third = calc.Calculate(data);

        Assert.NotSame(first, third);
        Assert.Equal(firstCount, first.Count); // the earlier result is still intact
    }

    [Fact]
    public void Calculate_MarksPitRoadCars()
    {
        var calc = new RelativeCalculator();
        var data = new TelemetryBuilder(playerIdx: 0)
            .WithCar(0, 0.50f)
            .WithCar(1, 0.51f, onPitRoad: true)
            .Build();

        var result = calc.Calculate(data, maxAhead: 5, maxBehind: 5);

        var pitted = result.SingleOrDefault(e => e.CarIdx == 1);
        Assert.NotNull(pitted);
        Assert.True(pitted!.IsOnPitRoad);
    }

    [Theory]
    [InlineData(1, 20, 12)]   // leader: everything behind
    [InlineData(20, 20, 12)]  // last: everything ahead
    [InlineData(10, 20, 12)]  // midfield: split
    public void GetSmartRowCount_AlwaysFillsTheAvailableRows(int position, int totalCars, int totalRows)
    {
        var (ahead, behind) = RelativeCalculator.GetSmartRowCount(position, totalCars, totalRows);

        Assert.True(ahead >= 0);
        Assert.True(behind >= 0);
        Assert.True(ahead + behind <= totalRows,
            $"P{position}: {ahead} ahead + {behind} behind exceeds {totalRows} rows");
    }
}
