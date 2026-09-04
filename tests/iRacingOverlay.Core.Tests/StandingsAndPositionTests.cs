using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

public class StandingsCalculatorTests
{
    [Fact]
    public void Calculate_WithoutPositionData_ReturnsEmpty()
    {
        var calc = new StandingsCalculator();

        var result = calc.Calculate(new TelemetryData());

        Assert.Empty(result);
    }

    [Fact]
    public void Calculate_OrdersByOverallPosition()
    {
        var calc = new StandingsCalculator();
        var data = new TelemetryBuilder(playerIdx: 0)
            .WithCar(0, 0.10f, position: 3)
            .WithCar(1, 0.50f, position: 1)
            .WithCar(2, 0.30f, position: 2)
            .Build();

        var result = calc.Calculate(data);

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(e => e.OverallPosition));
    }

    [Fact]
    public void Calculate_SkipsUnclassifiedCars()
    {
        var calc = new StandingsCalculator();
        var data = new TelemetryBuilder(playerIdx: 0)
            .WithCar(0, 0.10f, position: 1)
            .WithCar(1, 0.20f, position: 2)
            .Build();

        // Car 9 has position 0 (never classified) and must not appear.
        var result = calc.Calculate(data);

        Assert.DoesNotContain(result, e => e.CarIdx == 9);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Calculate_MarksThePlayer()
    {
        var calc = new StandingsCalculator();
        var data = new TelemetryBuilder(playerIdx: 1)
            .WithCar(0, 0.10f, position: 1)
            .WithCar(1, 0.20f, position: 2)
            .Build();

        var result = calc.Calculate(data);

        Assert.Single(result, e => e.IsPlayer);
        Assert.Equal(1, result.Single(e => e.IsPlayer).CarIdx);
    }

    [Fact]
    public void Calculate_CountsAPitStopOncePerEntryToPitRoad()
    {
        var calc = new StandingsCalculator();

        var onTrack = new TelemetryBuilder(playerIdx: 0).WithCar(0, 0.10f, position: 1).Build();
        var inPits = new TelemetryBuilder(playerIdx: 0).WithCar(0, 0.10f, position: 1, onPitRoad: true).Build();

        calc.Calculate(onTrack);
        calc.Calculate(inPits);   // entering pit road — counts
        calc.Calculate(inPits);   // still on pit road — must not count again
        var result = calc.Calculate(inPits);

        Assert.Equal(1, result.Single(e => e.CarIdx == 0).PitStopCount);
    }

    [Fact]
    public void Calculate_RespectsMaxRows()
    {
        var calc = new StandingsCalculator();
        var data = new TelemetryBuilder(playerIdx: 0).WithEvenlySpacedField(20).Build();

        var result = calc.Calculate(data, maxRows: 5);

        Assert.True(result.Count <= 5, $"expected at most 5 rows, got {result.Count}");
    }
}

public class LivePositionCalculatorTests
{
    private const int StateRacing = 4;
    private const int StateCheckered = 5;
    private const int StateCoolDown = 6;

    [Theory]
    [InlineData(StateCheckered, PositionCalculationMode.Frozen)]
    [InlineData(StateCoolDown, PositionCalculationMode.Frozen)]
    [InlineData(StateRacing, PositionCalculationMode.RaceLaps)]
    public void DetermineMode_MapsSessionStateToMode(int sessionState, PositionCalculationMode expected)
    {
        var calc = new LivePositionCalculator();
        var data = new TelemetryBuilder().WithSessionState(sessionState).Build();

        Assert.Equal(expected, calc.DetermineMode(data));
    }

    [Fact]
    public void CalculateLivePosition_FreezesPositionOnceCheckeredFalls()
    {
        var calc = new LivePositionCalculator();

        var racing = new TelemetryBuilder(playerIdx: 0)
            .WithEvenlySpacedField(5)
            .WithSessionState(StateRacing)
            .With(d => d.Position = 2)
            .Build();

        int racingPosition = calc.CalculateLivePosition(racing, classOnly: false);

        // Checkered drops, then the player keeps circulating and drops back —
        // the displayed position must not move.
        var checkered = new TelemetryBuilder(playerIdx: 0)
            .WithEvenlySpacedField(5)
            .WithSessionState(StateCheckered)
            .With(d => d.Position = 5)
            .Build();

        calc.CalculateLivePosition(racing, classOnly: false); // transition tick
        int frozen = calc.CalculateLivePosition(checkered, classOnly: false);

        Assert.True(frozen > 0);
        Assert.Equal(racingPosition, frozen);
    }

    [Fact]
    public void CalculateLivePosition_NeverReturnsZero()
    {
        var calc = new LivePositionCalculator();
        var data = new TelemetryBuilder(playerIdx: 0)
            .WithSessionState(1) // GetInCar — before anything is classified
            .Build();

        Assert.True(calc.CalculateLivePosition(data, classOnly: false) >= 1);
    }
}
