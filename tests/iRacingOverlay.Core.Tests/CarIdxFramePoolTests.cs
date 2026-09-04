using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// The frame pool exists to stop widgets reading per-car arrays that the next
/// telemetry tick has already overwritten. These tests pin the two properties
/// that guarantee makes: consecutive ticks get different buffers, and every
/// copy zeroes the tail so stale cars never linger.
/// </summary>
public class CarIdxFramePoolTests
{
    [Fact]
    public void Next_ReturnsADifferentFrameOnConsecutiveTicks()
    {
        var pool = new CarIdxFramePool();

        var first = pool.Next();
        var second = pool.Next();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Next_RotatesThroughTheWholePoolBeforeReusingAFrame()
    {
        var pool = new CarIdxFramePool();
        var seen = new List<CarIdxFrame>();

        // Depth is 4 — a frame handed out now must not come back before then,
        // which is the headroom that keeps an in-flight render safe.
        for (int i = 0; i < 4; i++)
            seen.Add(pool.Next());

        Assert.Equal(4, seen.Distinct().Count());
        Assert.Same(seen[0], pool.Next());
    }

    [Fact]
    public void Copy_ZeroesTheTailWhenTheSourceIsShorter()
    {
        var destination = new int[CarIdxFrame.MaxCars];
        Array.Fill(destination, 99); // stale data from a previous tick

        CarIdxFrame.Copy(new[] { 1, 2, 3 }, destination);

        Assert.Equal(1, destination[0]);
        Assert.Equal(3, destination[2]);
        Assert.All(destination.Skip(3), v => Assert.Equal(0, v));
    }

    [Fact]
    public void Copy_ClearsEverythingWhenTheSourceIsNull()
    {
        var destination = new float[CarIdxFrame.MaxCars];
        Array.Fill(destination, 1.5f);

        CarIdxFrame.Copy<float>(null, destination);

        Assert.All(destination, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void Copy_TruncatesWhenTheSourceIsLongerThanTheBuffer()
    {
        var destination = new int[CarIdxFrame.MaxCars];
        var oversized = Enumerable.Range(0, CarIdxFrame.MaxCars + 16).ToArray();

        var exception = Record.Exception(() => CarIdxFrame.Copy(oversized, destination));

        Assert.Null(exception);
        Assert.Equal(CarIdxFrame.MaxCars - 1, destination[CarIdxFrame.MaxCars - 1]);
    }

    private enum TestSurface { NotInWorld = -1, OffTrack = 0, OnTrack = 3 }

    [Fact]
    public void CopyEnum_WidensEnumValuesToInt()
    {
        var destination = new int[CarIdxFrame.MaxCars];
        Array.Fill(destination, 7);

        CarIdxFrame.CopyEnum(new[] { TestSurface.OnTrack, TestSurface.NotInWorld }, destination);

        Assert.Equal(3, destination[0]);
        Assert.Equal(-1, destination[1]);
        Assert.All(destination.Skip(2), v => Assert.Equal(0, v));
    }
}
