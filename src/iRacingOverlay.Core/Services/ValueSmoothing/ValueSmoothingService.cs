using System;
using System.Collections.Generic;

namespace iRacingOverlay.Core.Services.ValueSmoothing;

/// <summary>
/// Service for smoothing rapidly changing telemetry values to prevent UI flickering
/// Uses exponential moving average (EMA) with configurable alpha and update thresholds
/// </summary>
public class ValueSmoothingService
{
    /// <summary>
    /// Configuration for a smoothed value
    /// </summary>
    public class SmoothingConfig
    {
        /// <summary>Smoothing factor (0-1): 0=no smoothing, 1=instant update</summary>
        public float Alpha { get; set; } = 0.3f;

        /// <summary>Minimum change threshold to trigger update (prevents micro-flickering)</summary>
        public float UpdateThreshold { get; set; } = 0.1f;

        /// <summary>Time-based reset threshold (seconds): reset if no update for this long</summary>
        public double ResetAfterSeconds { get; set; } = 5.0;
    }

    /// <summary>
    /// Smoothed value state
    /// </summary>
    private class SmoothedValue
    {
        public float CurrentSmoothed { get; set; }
        public float LastRawValue { get; set; }
        public DateTime LastUpdate { get; set; }
        public SmoothingConfig Config { get; set; }

        public SmoothedValue(SmoothingConfig config)
        {
            Config = config;
            LastUpdate = DateTime.UtcNow;
        }
    }

    private readonly Dictionary<string, SmoothedValue> _smoothedValues = new();

    /// <summary>
    /// Smooth a value using exponential moving average with update threshold
    /// </summary>
    /// <param name="key">Unique identifier for this value stream</param>
    /// <param name="rawValue">New raw value to smooth</param>
    /// <param name="config">Smoothing configuration (optional, uses defaults if null)</param>
    /// <returns>Smoothed value</returns>
    public float Smooth(string key, float rawValue, SmoothingConfig? config = null)
    {
        config ??= new SmoothingConfig(); // Use defaults if not specified

        // Get or create smoothed value state
        if (!_smoothedValues.TryGetValue(key, out var state))
        {
            state = new SmoothedValue(config)
            {
                CurrentSmoothed = rawValue,
                LastRawValue = rawValue
            };
            _smoothedValues[key] = state;
            return rawValue; // First value - no smoothing needed
        }

        // Check for reset condition (stale data)
        var timeSinceUpdate = (DateTime.UtcNow - state.LastUpdate).TotalSeconds;
        if (timeSinceUpdate > config.ResetAfterSeconds)
        {
            // Reset to raw value after long gap
            state.CurrentSmoothed = rawValue;
            state.LastRawValue = rawValue;
            state.LastUpdate = DateTime.UtcNow;
            return rawValue;
        }

        // Calculate change from last raw value
        float delta = Math.Abs(rawValue - state.LastRawValue);

        // Only update if change exceeds threshold (prevents micro-flickering)
        if (delta < config.UpdateThreshold)
        {
            return state.CurrentSmoothed; // Return existing smoothed value
        }

        // Apply exponential moving average
        // Formula: EMA = (alpha * new) + ((1-alpha) * previous)
        float smoothed = (config.Alpha * rawValue) + ((1 - config.Alpha) * state.CurrentSmoothed);

        // Update state
        state.CurrentSmoothed = smoothed;
        state.LastRawValue = rawValue;
        state.LastUpdate = DateTime.UtcNow;

        return smoothed;
    }

    /// <summary>
    /// Get current smoothed value without updating (useful for display)
    /// </summary>
    public float? GetCurrent(string key)
    {
        if (_smoothedValues.TryGetValue(key, out var state))
        {
            return state.CurrentSmoothed;
        }
        return null;
    }

    /// <summary>
    /// Reset smoothing for a specific key
    /// </summary>
    public void Reset(string key)
    {
        _smoothedValues.Remove(key);
    }

    /// <summary>
    /// Reset all smoothed values
    /// </summary>
    public void ResetAll()
    {
        _smoothedValues.Clear();
    }

    /// <summary>
    /// Predefined configuration for gap values (position tracking)
    /// </summary>
    public static SmoothingConfig GapSmoothingConfig => new()
    {
        Alpha = 0.25f,           // Smooth over ~4 samples (slow smoothing for stable display)
        UpdateThreshold = 0.1f,  // Only update if gap changes by 0.1s or more
        ResetAfterSeconds = 3.0  // Reset if no update for 3 seconds (pit entry, etc.)
    };

    /// <summary>
    /// Predefined configuration for lap time values
    /// </summary>
    public static SmoothingConfig LapTimeSmoothingConfig => new()
    {
        Alpha = 0.4f,            // Moderate smoothing
        UpdateThreshold = 0.05f, // Update for 0.05s changes
        ResetAfterSeconds = 5.0
    };

    /// <summary>
    /// Predefined configuration for position values (more responsive)
    /// </summary>
    public static SmoothingConfig PositionSmoothingConfig => new()
    {
        Alpha = 0.5f,            // Faster response for position changes
        UpdateThreshold = 0.5f,  // Only update for significant position changes
        ResetAfterSeconds = 2.0
    };
}
