using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// The adjustment overlay generalises what was a hardcoded brake-bias display.
/// The property that makes that safe is edge-triggering: cars without a given
/// control publish nothing for it, so it reads a constant zero and never fires.
/// </summary>
public class CarAdjustmentTrackerTests
{
    [Fact]
    public void FirstFrameEstablishesBaseline_AndDoesNotFire()
    {
        // Otherwise every adjuster would announce itself the moment the driver
        // gets into the car.
        var tracker = new CarAdjustmentTracker();
        var data = new TelemetryData { BrakeBias = 54.5f };

        Assert.False(tracker.Update(data));
        Assert.Null(tracker.CurrentLabel);
    }

    [Fact]
    public void ReportsBrakeBiasChange()
    {
        var tracker = new CarAdjustmentTracker();

        tracker.Update(new TelemetryData { BrakeBias = 54.5f });
        bool fired = tracker.Update(new TelemetryData { BrakeBias = 55.0f });

        Assert.True(fired);
        Assert.Equal("BRAKE BIAS", tracker.CurrentLabel);
        Assert.Equal("55.0%", tracker.CurrentValue);
    }

    [Fact]
    public void ControlsAbsentOnThisCarNeverFire()
    {
        // A car with no anti-roll adjuster reports a constant zero for it.
        var tracker = new CarAdjustmentTracker();
        var data = new TelemetryData { AntiRollFront = 0f, FuelMixture = 0f };

        tracker.Update(data);

        Assert.False(tracker.Update(data));
        Assert.False(tracker.Update(data));
        Assert.Null(tracker.CurrentLabel);
    }

    [Fact]
    public void SmallFluctuationsAreNotTreatedAsAdjustments()
    {
        var tracker = new CarAdjustmentTracker();

        tracker.Update(new TelemetryData { BrakeBias = 54.50f });

        // Below the 0.05 tolerance — sensor noise, not a deliberate change.
        Assert.False(tracker.Update(new TelemetryData { BrakeBias = 54.52f }));
    }

    [Fact]
    public void ReportsTheMostRecentControl()
    {
        var tracker = new CarAdjustmentTracker();

        var baseline = new TelemetryData { BrakeBias = 54f, TractionControl = 3 };
        tracker.Update(baseline);

        tracker.Update(new TelemetryData { BrakeBias = 55f, TractionControl = 3 });
        Assert.Equal("BRAKE BIAS", tracker.CurrentLabel);

        tracker.Update(new TelemetryData { BrakeBias = 55f, TractionControl = 5 });
        Assert.Equal("TC", tracker.CurrentLabel);
        Assert.Equal("5", tracker.CurrentValue);
    }

    [Fact]
    public void FormatsTractionControlOffAsWord()
    {
        var tracker = new CarAdjustmentTracker();

        tracker.Update(new TelemetryData { TractionControl = 4 });
        tracker.Update(new TelemetryData { TractionControl = 0 });

        Assert.Equal("TC", tracker.CurrentLabel);
        Assert.Equal("OFF", tracker.CurrentValue);
    }

    [Fact]
    public void FormatsBooleanControlsAsOnOff()
    {
        var tracker = new CarAdjustmentTracker();

        tracker.Update(new TelemetryData { PowerSteeringEnabled = false });
        tracker.Update(new TelemetryData { PowerSteeringEnabled = true });

        Assert.Equal("POWER STEER", tracker.CurrentLabel);
        Assert.Equal("ON", tracker.CurrentValue);
    }

    [Fact]
    public void Reset_ClearsBaselinesAndCurrentValue()
    {
        var tracker = new CarAdjustmentTracker();

        tracker.Update(new TelemetryData { BrakeBias = 54f });
        tracker.Update(new TelemetryData { BrakeBias = 56f });
        Assert.NotNull(tracker.CurrentLabel);

        tracker.Reset();

        Assert.Null(tracker.CurrentLabel);
        // The next frame is a fresh baseline, so it must not fire.
        Assert.False(tracker.Update(new TelemetryData { BrakeBias = 60f }));
    }
}
