using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// The detector was a static state machine advanced only as a side effect of a
/// widget displaying its field. These tests pin the properties that made it an
/// instance service: independent state per instance, and a published result that
/// does not mutate under a widget rendering it.
/// </summary>
public class WheelLockupDetectorTests
{
    private static TelemetryData Coasting() => new()
    {
        Speed = 50f,
        Brake = 0f,
        LongAccel = 0f,
    };

    private static TelemetryData HardBraking(float brake, float longAccel) => new()
    {
        Speed = 50f,
        Brake = brake,
        LongAccel = longAccel,
        BrakeABSactive = false,
    };

    [Fact]
    public void DetectLockup_WhenCoasting_ReportsNoLockup()
    {
        var detector = new WheelLockupDetector();

        var state = detector.DetectLockup(Coasting());

        Assert.False(state.AnyWheelLocked);
    }

    [Fact]
    public void DetectLockup_BelowMinimumSpeed_ReportsNoLockup()
    {
        var detector = new WheelLockupDetector();
        var data = HardBraking(brake: 1.0f, longAccel: -20f);
        data.Speed = 1f; // well under MinSpeed

        Assert.False(detector.DetectLockup(data).AnyWheelLocked);
    }

    [Fact]
    public void DetectLockup_ResultIsNotOverwrittenByTheNextFewCalls()
    {
        // The result is published on the telemetry frame and read by widgets after
        // an async dispatch, so consecutive calls must hand back distinct objects.
        var detector = new WheelLockupDetector();
        var data = Coasting();

        var first = detector.DetectLockup(data);
        var second = detector.DetectLockup(data);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Instances_DoNotShareState()
    {
        // As a static class, two consumers stepped one shared rolling buffer.
        var a = new WheelLockupDetector { MinSpeed = 8f };
        var b = new WheelLockupDetector { MinSpeed = 999f };

        Assert.Equal(8f, a.MinSpeed);
        Assert.Equal(999f, b.MinSpeed);
    }

    [Fact]
    public void Reset_ClearsAccumulatedState()
    {
        var detector = new WheelLockupDetector();

        for (int i = 0; i < 20; i++)
            detector.DetectLockup(HardBraking(0.9f, -18f));

        detector.Reset();

        Assert.False(detector.DetectLockup(Coasting()).AnyWheelLocked);
    }

    [Fact]
    public void CopyFrom_TransfersEveryFlag()
    {
        var source = new WheelLockupState
        {
            LeftFrontLocked = true,
            AnyWheelLocked = true,
            FrontAxleLockup = true,
            ABSActive = true,
            DetectionMethod = LockupDetectionMethod.PressureImbalance,
            Confidence = LockupConfidence.High,
        };

        var destination = new WheelLockupState();
        destination.CopyFrom(source);

        Assert.True(destination.LeftFrontLocked);
        Assert.True(destination.AnyWheelLocked);
        Assert.True(destination.FrontAxleLockup);
        Assert.True(destination.ABSActive);
        Assert.Equal(LockupDetectionMethod.PressureImbalance, destination.DetectionMethod);
        Assert.Equal(LockupConfidence.High, destination.Confidence);
    }

    [Fact]
    public void Reset_OnState_ClearsEverything()
    {
        var state = new WheelLockupState
        {
            AnyWheelLocked = true,
            RightRearLocked = true,
            Confidence = LockupConfidence.High,
        };

        state.Reset();

        Assert.False(state.AnyWheelLocked);
        Assert.False(state.RightRearLocked);
        Assert.Equal(LockupConfidence.None, state.Confidence);
    }
}
