using System;
using System.Globalization;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Watches every in-car adjustment the sim exposes and reports whichever one the
/// driver most recently changed.
///
/// MRT One already had a brake-bias overlay that flashed the new value on change.
/// That was one hardcoded channel; the sim publishes a dozen. Generalising it
/// turns a single feature into a complete one and reuses the display and auto-hide
/// timer that were already written.
///
/// Every adjuster except brake bias is car-dependent — a car without an ABS knob
/// simply does not publish <c>dcABS</c>, and it reads zero. That is handled for
/// free by edge-triggering: a channel that never changes never fires, so a car
/// without a control silently has nothing to show rather than displaying a
/// misleading "ABS 0".
/// </summary>
public sealed class CarAdjustmentTracker
{
    /// <summary>One adjustable control being watched.</summary>
    private sealed class Adjustment
    {
        public required string Label { get; init; }
        public required Func<TelemetryData, float> Read { get; init; }
        public required Func<float, string> Format { get; init; }

        /// <summary>Change smaller than this is noise, not a deliberate adjustment.</summary>
        public float Tolerance { get; init; } = 0.05f;

        public float Last;
        public bool Initialised;
    }

    private readonly Adjustment[] _adjustments;

    /// <summary>The adjustment changed most recently, or null if none has changed yet.</summary>
    public string? CurrentLabel { get; private set; }

    /// <summary>Formatted value of the most recent adjustment.</summary>
    public string? CurrentValue { get; private set; }

    /// <summary>When the most recent adjustment happened.</summary>
    public DateTime? ChangedAt { get; private set; }

    public CarAdjustmentTracker()
    {
        _adjustments = new[]
        {
            new Adjustment
            {
                Label = "BRAKE BIAS",
                Read = d => d.BrakeBias,
                Format = v => v.ToString("F1", CultureInfo.InvariantCulture) + "%",
            },
            new Adjustment
            {
                Label = "TC",
                Read = d => d.TractionControl,
                Format = v => v <= 0 ? "OFF" : v.ToString("F0", CultureInfo.InvariantCulture),
                Tolerance = 0.4f, // integer levels
            },
            new Adjustment
            {
                Label = "ABS",
                Read = d => d.AbsSetting,
                Format = v => v <= 0 ? "OFF" : v.ToString("F0", CultureInfo.InvariantCulture),
                Tolerance = 0.4f,
            },
            new Adjustment
            {
                Label = "MIXTURE",
                Read = d => d.FuelMixture,
                Format = v => v.ToString("F0", CultureInfo.InvariantCulture),
                Tolerance = 0.4f,
            },
            new Adjustment
            {
                Label = "THROTTLE MAP",
                Read = d => d.ThrottleShape,
                Format = v => v.ToString("F0", CultureInfo.InvariantCulture),
                Tolerance = 0.4f,
            },
            new Adjustment
            {
                Label = "ARB FRONT",
                Read = d => d.AntiRollFront,
                Format = v => v.ToString("F0", CultureInfo.InvariantCulture),
                Tolerance = 0.4f,
            },
            new Adjustment
            {
                Label = "ARB REAR",
                Read = d => d.AntiRollRear,
                Format = v => v.ToString("F0", CultureInfo.InvariantCulture),
                Tolerance = 0.4f,
            },
            new Adjustment
            {
                Label = "WEIGHT JACKER",
                Read = d => d.WeightJackerRight,
                Format = v => v.ToString("F1", CultureInfo.InvariantCulture),
            },
            new Adjustment
            {
                Label = "POWER STEER",
                Read = d => d.PowerSteeringEnabled ? 1f : 0f,
                Format = v => v > 0 ? "ON" : "OFF",
                Tolerance = 0.4f,
            },
            new Adjustment
            {
                Label = "LAUNCH RPM",
                Read = d => d.LaunchRPM,
                Format = v => v.ToString("F0", CultureInfo.InvariantCulture),
                Tolerance = 50f,
            },
        };
    }

    /// <summary>
    /// Evaluate this frame. Returns true when an adjustment just changed, in which
    /// case <see cref="CurrentLabel"/> and <see cref="CurrentValue"/> describe it.
    /// </summary>
    public bool Update(TelemetryData data)
    {
        bool changed = false;

        foreach (var adj in _adjustments)
        {
            float value = adj.Read(data);

            // The first value is the baseline, not a change — otherwise every
            // adjustment would fire the moment the driver gets in the car.
            if (!adj.Initialised)
            {
                adj.Last = value;
                adj.Initialised = true;
                continue;
            }

            if (Math.Abs(value - adj.Last) <= adj.Tolerance) continue;

            adj.Last = value;

            // Last writer wins: if two changed on the same frame, show the later
            // one in the list. In practice a driver moves one control at a time.
            CurrentLabel = adj.Label;
            CurrentValue = adj.Format(value);
            ChangedAt = DateTime.UtcNow;
            changed = true;
        }

        return changed;
    }

    /// <summary>Forget all baselines. Call on disconnect or when changing car.</summary>
    public void Reset()
    {
        foreach (var adj in _adjustments)
        {
            adj.Initialised = false;
            adj.Last = 0f;
        }

        CurrentLabel = null;
        CurrentValue = null;
        ChangedAt = null;
    }
}
