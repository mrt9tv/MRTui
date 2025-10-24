using iRacingOverlay.Core.Models;

namespace iRacingOverlay.WPF.Widgets.MRTOneWidget;

/// <summary>
/// Manages state tracking for MRT One widget to prevent UI flicker
/// Caches previous values and detects changes for opacity/visibility updates
/// Extracted from MRTOneWidget to isolate state management logic
/// </summary>
public class MRTOneStateManager
{
    // ABS state tracking (prevent flicker from rapid updates)
    private int _lastLeftABSValue = -1;
    private int _lastRightABSValue = -1;

    // Traction Control state tracking (prevent flicker)
    private int _lastLeftTCValue = -999;
    private int _lastRightTCValue = -999;

    // Wheel Lockup state tracking (prevent flicker)
    private int _lastLeftLockupValue = -1;
    private int _lastRightLockupValue = -1;

    // Brake bias state tracking (detect changes for transient overlay)
    private float _lastBrakeBias = -1f;
    private bool _brakeBiasInitialized = false;

    // Proximity zone tracking (for radar blink detection)
    private ProximityZone _currentFrontZone = ProximityZone.Clear;
    private ProximityZone _currentRearZone = ProximityZone.Clear;

    // Pit limiter state (for indicator blinking)
    private bool _isPitLimiterActive = false;

    #region ABS State Management

    /// <summary>
    /// Check ABS state and return opacity changes (prevents flicker)
    /// Returns tuple: (leftOpacity, rightOpacity, hasChanged)
    /// </summary>
    public OpacityState GetABSOpacity(int leftABS, int rightABS)
    {
        bool leftChanged = leftABS != _lastLeftABSValue;
        bool rightChanged = rightABS != _lastRightABSValue;

        if (leftChanged || rightChanged)
        {
            _lastLeftABSValue = leftABS;
            _lastRightABSValue = rightABS;

            // ABS opacity: 0.3 (inactive) → 1.0 (active)
            double leftOpacity = leftABS > 0 ? 1.0 : 0.3;
            double rightOpacity = rightABS > 0 ? 1.0 : 0.3;

            return new OpacityState(leftOpacity, rightOpacity, true);
        }

        return new OpacityState(0, 0, false); // No change
    }

    #endregion

    #region Traction Control State Management

    /// <summary>
    /// Check TC state and return opacity changes (prevents flicker)
    /// TC scale is car-specific: some cars 0=OFF, others 12=OFF
    /// </summary>
    public OpacityState GetTCOpacity(int leftTC, int rightTC)
    {
        bool leftChanged = leftTC != _lastLeftTCValue;
        bool rightChanged = rightTC != _lastRightTCValue;

        if (leftChanged || rightChanged)
        {
            _lastLeftTCValue = leftTC;
            _lastRightTCValue = rightTC;

            // TC opacity: 0.3 (OFF/N/A) → 1.0 (enabled)
            // Consider <0 as N/A, 0 as OFF, >0 as active
            double leftOpacity = leftTC <= 0 ? 0.3 : 1.0;
            double rightOpacity = rightTC <= 0 ? 0.3 : 1.0;

            return new OpacityState(leftOpacity, rightOpacity, true);
        }

        return new OpacityState(0, 0, false); // No change
    }

    #endregion

    #region Wheel Lockup State Management

    /// <summary>
    /// Check wheel lockup state and return opacity changes (prevents flicker)
    /// </summary>
    public OpacityState GetLockupOpacity(int leftLockup, int rightLockup)
    {
        bool leftChanged = leftLockup != _lastLeftLockupValue;
        bool rightChanged = rightLockup != _lastRightLockupValue;

        if (leftChanged || rightChanged)
        {
            _lastLeftLockupValue = leftLockup;
            _lastRightLockupValue = rightLockup;

            // Lockup opacity: 0.3 (no lockup) → 1.0 (locked)
            double leftOpacity = leftLockup > 0 ? 1.0 : 0.3;
            double rightOpacity = rightLockup > 0 ? 1.0 : 0.3;

            return new OpacityState(leftOpacity, rightOpacity, true);
        }

        return new OpacityState(0, 0, false); // No change
    }

    #endregion

    #region Brake Bias State Management

    /// <summary>
    /// Check brake bias for changes and determine if overlay should show
    /// Prevents showing on initial connection
    /// </summary>
    public BrakeBiasChange CheckBrakeBiasChange(float currentBias)
    {
        // First value received - don't trigger overlay
        if (!_brakeBiasInitialized)
        {
            _lastBrakeBias = currentBias;
            _brakeBiasInitialized = true;
            return new BrakeBiasChange(false, false, currentBias);
        }

        // Check if value changed (tolerance of 0.05%)
        bool hasChanged = Math.Abs(currentBias - _lastBrakeBias) > 0.05f;

        if (hasChanged)
        {
            _lastBrakeBias = currentBias;
            return new BrakeBiasChange(true, true, currentBias);
        }

        return new BrakeBiasChange(false, false, currentBias);
    }

    #endregion

    #region Proximity Zone State Management

    /// <summary>
    /// Update proximity zones and return if blink state should change
    /// </summary>
    public ProximityZoneUpdate UpdateProximityZones(ProximityZone frontZone, ProximityZone rearZone)
    {
        bool frontChanged = frontZone != _currentFrontZone;
        bool rearChanged = rearZone != _currentRearZone;

        _currentFrontZone = frontZone;
        _currentRearZone = rearZone;

        return new ProximityZoneUpdate(
            frontZone,
            rearZone,
            frontChanged,
            rearChanged,
            frontZone == ProximityZone.VeryClose,
            rearZone == ProximityZone.VeryClose
        );
    }

    /// <summary>
    /// Get current proximity zones (for blink timer access)
    /// </summary>
    public (ProximityZone Front, ProximityZone Rear) GetCurrentZones()
    {
        return (_currentFrontZone, _currentRearZone);
    }

    #endregion

    #region Pit Limiter State Management

    /// <summary>
    /// Update pit limiter state and return if display should change
    /// </summary>
    public PitLimiterUpdate UpdatePitLimiter(bool isActive)
    {
        bool changed = isActive != _isPitLimiterActive;
        bool wasActive = _isPitLimiterActive;

        _isPitLimiterActive = isActive;

        return new PitLimiterUpdate(isActive, changed, wasActive);
    }

    /// <summary>
    /// Get current pit limiter state (for blink timer access)
    /// </summary>
    public bool IsPitLimiterActive => _isPitLimiterActive;

    #endregion

    #region Reset Methods

    /// <summary>
    /// Reset all cached state when disconnecting from iRacing
    /// Prevents stale data display on reconnection
    /// </summary>
    public void ResetAllState()
    {
        // Reset ABS state
        _lastLeftABSValue = -1;
        _lastRightABSValue = -1;

        // Reset TC state
        _lastLeftTCValue = -999;
        _lastRightTCValue = -999;

        // Reset wheel lockup state
        _lastLeftLockupValue = -1;
        _lastRightLockupValue = -1;

        // Reset brake bias state
        _lastBrakeBias = -1f;
        _brakeBiasInitialized = false;

        // Reset proximity zones
        _currentFrontZone = ProximityZone.Clear;
        _currentRearZone = ProximityZone.Clear;

        // Reset pit limiter
        _isPitLimiterActive = false;
    }

    #endregion
}

#region State Result Types

/// <summary>
/// Opacity state result with change detection
/// </summary>
/// <param name="LeftOpacity">Opacity for left side (0.3 or 1.0)</param>
/// <param name="RightOpacity">Opacity for right side (0.3 or 1.0)</param>
/// <param name="HasChanged">True if state changed since last check</param>
public record OpacityState(double LeftOpacity, double RightOpacity, bool HasChanged);

/// <summary>
/// Brake bias change result
/// </summary>
/// <param name="HasChanged">True if bias changed</param>
/// <param name="ShowOverlay">True if overlay should be shown</param>
/// <param name="Value">Current bias value</param>
public record BrakeBiasChange(bool HasChanged, bool ShowOverlay, float Value);

/// <summary>
/// Proximity zone update result
/// </summary>
/// <param name="FrontZone">Current front zone</param>
/// <param name="RearZone">Current rear zone</param>
/// <param name="FrontChanged">True if front zone changed</param>
/// <param name="RearChanged">True if rear zone changed</param>
/// <param name="FrontCritical">True if front is VeryClose (blink needed)</param>
/// <param name="RearCritical">True if rear is VeryClose (blink needed)</param>
public record ProximityZoneUpdate(
    ProximityZone FrontZone,
    ProximityZone RearZone,
    bool FrontChanged,
    bool RearChanged,
    bool FrontCritical,
    bool RearCritical
);

/// <summary>
/// Pit limiter update result
/// </summary>
/// <param name="IsActive">Current active state</param>
/// <param name="Changed">True if state changed</param>
/// <param name="WasActive">Previous active state</param>
public record PitLimiterUpdate(bool IsActive, bool Changed, bool WasActive);

#endregion
