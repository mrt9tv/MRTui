using iRacingOverlay.Core.Models;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// DECELERATION-BASED wheel lock-up detection system for iRacing telemetry
/// Uses longitudinal acceleration (deceleration) inefficiency to detect wheel lockup
/// PHYSICS PRINCIPLE: Locked wheels have LESS grip than rolling wheels
/// → Lockup causes deceleration to DROP or PLATEAU despite increased brake input
/// Works universally - no sensors required, pure physics-based detection
/// </summary>
public static class WheelLockupDetector
{
    // ===== DECELERATION TRACKING =====
    
    private static readonly Queue<DecelSample> _decelHistory = new(capacity: 15);
    private static float _maxDecelThisStop = 0f;
    private static bool _isHardBraking = false;
    private static bool _wasLocked = false; // Track if wheels were locked in previous frame
    private static int _unlockConfirmFrames = 0; // Frames of good decel since potential unlock
    
    private class DecelSample
    {
        public float Brake { get; set; }
        public float Decel { get; set; }  // Positive value for deceleration (LongAccel)
        public float LatAccel { get; set; } // Lateral acceleration for left/right detection
        public float Speed { get; set; }
        public double Time { get; set; }
        public float Yaw { get; set; }  // Car rotation for understeer detection
        public float SteeringAngle { get; set; } // Steering input
    }
    
    // ===== DETECTION THRESHOLDS (Configurable) =====
    
    /// <summary>Enable diagnostic logging to console (for debugging)</summary>
    public static bool EnableDiagnostics { get; set; } = true;
    
    /// <summary>Minimum vehicle speed (m/s) - 8 m/s = 29 km/h (BALANCED AGGRESSIVE)</summary>
    public static float MinSpeed { get; set; } = 8.0f;
    
    /// <summary>Minimum brake input (0-1) - 8% brake minimum (BALANCED AGGRESSIVE)</summary>
    public static float MinBrakeInput { get; set; } = 0.08f;
    
    /// <summary>
    /// Deceleration efficiency drop threshold
    /// If decel drops by this percentage while brake input increases → LOCKUP
    /// Default 0.10 = 10% deceleration drop (BALANCED AGGRESSIVE)
    /// Example: Was decelerating at 20 m/s², now only 18.0 m/s² despite more brake → LOCKED
    /// </summary>
    public static float DecelDropThreshold { get; set; } = 0.10f;
    
    /// <summary>
    /// Brake input increase required to detect inefficiency
    /// Must be braking harder by at least this much to compare efficiency
    /// Default 0.03 = 3% more brake input (BALANCED AGGRESSIVE)
    /// </summary>
    public static float BrakeIncreaseThreshold { get; set; } = 0.03f;
    
    // ===== PUBLIC DETECTION METHOD =====
    
    /// <summary>
    /// Detect wheel lockup using deceleration inefficiency analysis
    /// </summary>
    public static WheelLockupState DetectLockup(TelemetryData data)
    {
        var state = new WheelLockupState();
        
        // Early exit if not braking hard enough or moving fast enough
        // CRITICAL: If already hard braking, continue detection even if brake input drops!
        // (locked wheels can remain locked even at low brake %)
        bool shouldContinue = _isHardBraking && data.Brake > 0.05f && data.Speed > MinSpeed * 0.5f;
        
        if ((data.Brake < MinBrakeInput || data.Speed < MinSpeed) && !shouldContinue)
        {
            if (EnableDiagnostics && data.Brake > 0.10f && data.Speed > 5f)
            {
                Console.WriteLine($"[EARLY EXIT] Brake:{data.Brake:F2} (min:{MinBrakeInput:F2}) Speed:{data.Speed:F1}m/s (min:{MinSpeed:F1}m/s) Decel:{-data.LongAccel:F1}m/s²");
            }
            
            // Reset tracking when not braking at all
            if (data.Brake < 0.05f)
            {
                _decelHistory.Clear();
                _maxDecelThisStop = 0f;
                _isHardBraking = false;
                _wasLocked = false;
                _unlockConfirmFrames = 0;
            }
            
            return state;
        }
        
        // Handle ABS detection (informational only)
        if (data.BrakeABSactive)
        {
            state.ABSActive = true;
            state.ABSPreventingLockup = true;
            // Don't return - continue checking if lockup occurred despite ABS
        }
        
        // Convert LongAccel to deceleration (make positive for braking)
        // LongAccel is negative during braking, we want positive decel values
        float decel = -data.LongAccel;
        float latAccel = data.LatAccel; // Lateral G-force (positive = right turn)
        
        // Add current sample to history
        var sample = new DecelSample
        {
            Brake = data.Brake,
            Decel = decel,
            LatAccel = latAccel,
            Speed = data.Speed,
            Time = data.SessionTime,
            Yaw = data.Yaw,
            SteeringAngle = data.SteeringWheelAngle
        };
        
        _decelHistory.Enqueue(sample);
        
        // Keep only recent history (15 samples @ 60Hz = 250ms)
        while (_decelHistory.Count > 15)
        {
            _decelHistory.Dequeue();
        }
        
        // Track maximum deceleration achieved during this braking event
        if (decel > _maxDecelThisStop)
        {
            _maxDecelThisStop = decel;
        }
        
        // Mark as hard braking (VERY AGGRESSIVE)
        if (!_isHardBraking && data.Brake > 0.18f)
        {
            _isHardBraking = true;
            if (EnableDiagnostics)
            {
                Console.WriteLine($"\n[HARD BRAKING START] Brake:{data.Brake:F2} Speed:{data.Speed:F1}m/s Decel:{decel:F2}m/s² ABS:{data.BrakeABSactive}");
            }
        }
        
        // Need at least 2 samples for comparison (33ms of history - MAXIMUM SPEED)
        if (_decelHistory.Count < 2)
        {
            return state;
        }
        
        // Performance: Work with queue directly instead of converting to array
        int count = _decelHistory.Count;
        
        // ===== UNLOCK DETECTION (PRIORITY #0 - Check FIRST!) =====
        // Only check unlock if wheels were previously locked
        // Wheels have unlocked when deceleration efficiency returns to normal
        // CRITICAL: Check unlock regardless of _isHardBraking state (user might have reduced brake)
        if (_wasLocked && count >= 2)
        {
            // Performance: Get last 2 samples directly
            var recentSamples = _decelHistory.Skip(count - 2).Take(2).ToArray();
            float avgBrake = (recentSamples[0].Brake + recentSamples[1].Brake) * 0.5f;
            float avgDecel = (recentSamples[0].Decel + recentSamples[1].Decel) * 0.5f;
            
            // Check if decel efficiency is back to normal
            float expectedDecel = avgBrake * 18.0f; // GT3 baseline (~18 m/s² at 100%)
            float efficiency = expectedDecel > 1.0f ? (avgDecel / expectedDecel) : 1.0f;
            
            // UNLOCK CONDITION: Good efficiency (60%+) OR low brake with reasonable decel
            // Instant unlock (17ms response - ULTRA FAST)
            bool goodEfficiency = efficiency > 0.60f && avgDecel > 1.5f;
            bool lowBrakeUnlock = avgBrake < 0.12f && avgDecel > 0.5f; // If brake < 12% and some decel, assume unlocked
            
            if (goodEfficiency || lowBrakeUnlock)
            {
                _wasLocked = false;
                _unlockConfirmFrames = 0;
                
                if (EnableDiagnostics)
                {
                    string reason = goodEfficiency ? "Good efficiency" : "Low brake";
                    Console.WriteLine($"✅ UNLOCK! ({reason}) Efficiency:{efficiency:P0} Decel:{avgDecel:F1}m/s² Brake:{avgBrake:P0}");
                }
                
                return state; // AnyWheelLocked = false
            }
            else
            {
                _unlockConfirmFrames = 0; // Reset counter if efficiency drops
            }
        }
        
        // ===== DECELERATION INEFFICIENCY DETECTION =====
        // Compare recent samples: if brake input increased but decel decreased/plateaued → LOCKUP
        // Performance: Compare newest vs oldest sample (33ms window - MAXIMUM SPEED)
        if (count >= 2)
        {
            var allSamples = _decelHistory.ToArray();
            
            // Old sample (first in history)
            float oldAvgBrake = allSamples[0].Brake;
            float oldAvgDecel = allSamples[0].Decel;
            
            // New sample (last in history - instant response)
            float newAvgBrake = allSamples[count-1].Brake;
            float newAvgDecel = allSamples[count-1].Decel;
            
            float brakeIncrease = newAvgBrake - oldAvgBrake;
            float decelRatio = oldAvgDecel > 0.1f ? newAvgDecel / oldAvgDecel : 1.0f;
            
            // SPEED-DEPENDENT THRESHOLD: Less sensitive at low speeds
            // Below 60 km/h (~16.7 m/s), require more severe drop to avoid false positives
            float speedFactor = data.Speed < 16.7f ? 1.15f : 1.0f; // 15% less sensitive below 60 km/h
            float adjustedDropThreshold = DecelDropThreshold * speedFactor;
            
            // LOCKUP CONDITION: Brake input increased significantly but deceleration dropped
            if (brakeIncrease > BrakeIncreaseThreshold && decelRatio < (1.0f - adjustedDropThreshold))
            {
                state.AnyWheelLocked = true;
                state.DetectionMethod = LockupDetectionMethod.DecelerationPlateau;
                state.Confidence = LockupConfidence.High;
                _wasLocked = true;
                _unlockConfirmFrames = 0;
                
                // Determine which wheels (pass last 2 samples)
                var newSamplesArray = new[] { allSamples[count-2], allSamples[count-1] };
                DetermineLockedWheels(state, data, newSamplesArray, decelRatio);
                
                if (EnableDiagnostics)
                {
                    string wheels = $"{(state.LeftFrontLocked ? "LF " : "")}{(state.RightFrontLocked ? "RF " : "")}{(state.LeftRearLocked ? "LR " : "")}{(state.RightRearLocked ? "RR " : "")}";
                    Console.WriteLine($"🔴 LOCKUP! Brake +{brakeIncrease:F2} decel {oldAvgDecel:F1}→{newAvgDecel:F1} ({decelRatio:F2}) | {wheels}");
                }
                
                return state;
            }
        }
        
        // ===== SUSTAINED LOCKUP DETECTION (PRIORITY #1 - Low/Medium Brake Lockup) =====
        // Catches wheels that lock at LOW-MEDIUM brake % during sustained braking
        // Uses strict 45% efficiency threshold to prevent false positives
        // HYSTERESIS: Unlock at 60%, re-trigger at 45% (15% gap prevents oscillation)
        // Performance: Pure efficiency check, no ratio comparisons
        if (_isHardBraking && data.Brake > 0.05f && _maxDecelThisStop > 6.0f)
        {
            // Calculate efficiency (cached calculation)
            float expectedDecelForBrake = data.Brake * 18.0f; // GT3 baseline
            float decelEfficiency = expectedDecelForBrake > 1.0f ? (decel / expectedDecelForBrake) : 1.0f;
            
            // HYSTERESIS: Much stricter than unlock (45% vs 60%) to prevent oscillation
            // Unlock at 60% efficiency, re-trigger only if drops below 45%
            // REMOVED belowMaxDecel condition - was causing false positives during brake modulation
            float sustainedEfficiencyThreshold = 0.45f; // Very strict to avoid false positives
            
            // Single condition: extremely low efficiency at low-medium brake
            bool lowBrakeLockup = data.Brake < 0.45f && decelEfficiency < sustainedEfficiencyThreshold;
            
            if (lowBrakeLockup)
            {
                state.AnyWheelLocked = true;
                state.DetectionMethod = LockupDetectionMethod.DecelerationPlateau;
                state.Confidence = LockupConfidence.High;
                _wasLocked = true;
                _unlockConfirmFrames = 0;
                
                // Determine which wheels (use last 2 samples for consistency)
                if (count >= 2)
                {
                    var allSamples = _decelHistory.ToArray();
                    var recentArray = new[] { allSamples[count-2], allSamples[count-1] };
                    DetermineLockedWheels(state, data, recentArray, decelEfficiency);
                }
                
                if (EnableDiagnostics)
                {
                    Console.WriteLine($"🔴 SUSTAINED! Brake:{data.Brake:P0} Decel:{decel:F1} (Exp:{expectedDecelForBrake:F1} Eff:{decelEfficiency:P0})");
                }
                
                return state;
            }
        }
        
        // ===== DECELERATION PLATEAU DETECTION (High Brake Alternative) =====
        // If braking hard but decel is significantly below maximum achieved → LOCKUP
        // Performance: Simple ratio check, no array operations
        if (_isHardBraking && data.Brake > 0.40f && _maxDecelThisStop > 6.0f)
        {
            float decelRatioVsMax = decel / _maxDecelThisStop;
            
            // SPEED-DEPENDENT: Less sensitive at low speeds
            float speedFactor = data.Speed < 16.7f ? 0.92f : 1.0f; // 0.74 below 60 km/h, 0.80 above
            float adjustedPlateauThreshold = 0.80f * speedFactor;
            
            // If current decel is < threshold of max decel achieved → wheels locked
            if (decelRatioVsMax < adjustedPlateauThreshold) // BALANCED AGGRESSIVE
            {
                state.AnyWheelLocked = true;
                state.DetectionMethod = LockupDetectionMethod.DecelerationPlateau;
                state.Confidence = LockupConfidence.Medium;
                _wasLocked = true;
                _unlockConfirmFrames = 0;
                
                // Determine which wheels (use last 2 samples)
                if (count >= 2)
                {
                    var allSamples = _decelHistory.ToArray();
                    var recentArray = new[] { allSamples[count-2], allSamples[count-1] };
                    DetermineLockedWheels(state, data, recentArray, decelRatioVsMax);
                }
                
                if (EnableDiagnostics)
                {
                    Console.WriteLine($"🔴 PLATEAU! Brake:{data.Brake:P0} Decel:{decel:F1} vs Max:{_maxDecelThisStop:F1} ({decelRatioVsMax:P0})");
                }
                
                return state;
            }
        }
        
        // ===== INSTANT DECELERATION DROP DETECTION (Efficiency Check) =====
        // Detect sudden deceleration drops based on brake vs decel efficiency
        // Can detect on FIRST sample if efficiency is very low (instant detection)
        // Performance: Use current decel directly instead of averaging
        if (data.Brake > 0.20f)
        {
            // Expected deceleration based on brake input
            float expectedDecel = data.Brake * 18.0f; // GT3 ~18 m/s² at 100% brake
            
            // SPEED-DEPENDENT: Less sensitive at low speeds
            float speedFactor = data.Speed < 16.7f ? 0.92f : 1.0f; // 0.60 below 60 km/h, 0.65 above
            float adjustedEfficiencyThreshold = 0.65f * speedFactor; // More aggressive for faster detection
            
            // If actual decel is much lower than expected → LOCKUP (AGGRESSIVE for speed)
            if (expectedDecel > 6.0f && decel < expectedDecel * adjustedEfficiencyThreshold) // More aggressive
            {
                state.AnyWheelLocked = true;
                state.DetectionMethod = LockupDetectionMethod.DecelerationPlateau;
                state.Confidence = LockupConfidence.Medium;
                _wasLocked = true;
                _unlockConfirmFrames = 0;
                
                // Determine which wheels (use last 2 samples for consistency)
                if (count >= 2)
                {
                    var allSamples = _decelHistory.ToArray();
                    var recentArray = new[] { allSamples[count-2], allSamples[count-1] };
                    float efficiencyRatio = decel / expectedDecel;
                    DetermineLockedWheels(state, data, recentArray, efficiencyRatio);
                }
                
                if (EnableDiagnostics)
                {
                    Console.WriteLine($"🔴 LOCKUP (Low Eff)! Brake:{data.Brake:P0} Expected:{expectedDecel:F1} Actual:{decel:F1} ({decel/expectedDecel:P0})");
                }
                
                return state;
            }
        }
        
        return state;
    }
    
    /// <summary>
    /// Determine which specific wheels are locked based on lateral acceleration and deceleration patterns
    /// </summary>
    private static void DetermineLockedWheels(WheelLockupState state, TelemetryData data, DecelSample[] recentSamples, float decelRatio)
    {
        float avgLatAccel = recentSamples.Average(s => Math.Abs(s.LatAccel));
        float avgSteer = Math.Abs(data.SteeringWheelAngle);
        
        // Analyze deceleration severity to estimate front vs rear
        // Severe deceleration drop (< 0.75) = likely rear lockup (more unstable)
        // Moderate drop (0.75-0.90) = could be front or mixed
        bool severeDecelDrop = decelRatio < 0.75f;
        bool moderateDecelDrop = decelRatio >= 0.75f && decelRatio < 0.90f;
        
        // ===== REAR WHEEL LOCKUP INDICATORS =====
        // Rear lockups cause instability, especially under hard braking
        if (severeDecelDrop)
        {
            // Severe decel drop = rear wheels likely locked (loss of rear grip)
            state.RearAxleLockup = true;
            state.LeftRearLocked = true;
            state.RightRearLocked = true;
            
            if (EnableDiagnostics)
            {
                Console.WriteLine($"  → REAR LOCKUP (Severe decel drop {decelRatio:F2})");
            }
        }
        
        // ===== FRONT WHEEL LOCKUP INDICATORS =====
        // Front lockups reduce steering effectiveness (understeer)
        // If steering during braking but low lateral accel = fronts locked
        if (moderateDecelDrop && avgSteer > 20.0f && avgLatAccel < 5.0f)
        {
            // Steering but no lateral grip = front wheels locked
            state.FrontAxleLockup = true;
            state.LeftFrontLocked = true;
            state.RightFrontLocked = true;
            
            if (EnableDiagnostics)
            {
                Console.WriteLine($"  → FRONT LOCKUP (Steer:{avgSteer:F1}° but LatAccel:{avgLatAccel:F1}m/s²)");
            }
        }
        
        // ===== LEFT vs RIGHT DETECTION =====
        // Use lateral acceleration + steering to detect asymmetric lockup
        float latAccel = recentSamples.Last().LatAccel;
        float steer = data.SteeringWheelAngle;
        
        // If steering right (positive) but getting left lateral accel (negative) = right wheel locked
        // If steering left (negative) but getting right lateral accel (positive) = left wheel locked
        if (Math.Abs(steer) > 30.0f && Math.Abs(latAccel) > 3.0f)
        {
            // Steering and lateral accel in opposite directions = locked wheel on steering side
            if ((steer > 0 && latAccel < -1.0f) || (steer < 0 && latAccel > 1.0f))
            {
                if (steer > 0) // Steering right, but car going left = right wheels locked
                {
                    if (state.FrontAxleLockup)
                    {
                        state.RightFrontLocked = true;
                        state.LeftFrontLocked = false;
                    }
                    if (state.RearAxleLockup)
                    {
                        state.RightRearLocked = true;
                        state.LeftRearLocked = false;
                    }
                    
                    if (EnableDiagnostics)
                    {
                        Console.WriteLine($"  → RIGHT SIDE LOCKUP (Steer right but LatAccel:{latAccel:F1})");
                    }
                }
                else // Steering left, but car going right = left wheels locked
                {
                    if (state.FrontAxleLockup)
                    {
                        state.LeftFrontLocked = true;
                        state.RightFrontLocked = false;
                    }
                    if (state.RearAxleLockup)
                    {
                        state.LeftRearLocked = true;
                        state.RightRearLocked = false;
                    }
                    
                    if (EnableDiagnostics)
                    {
                        Console.WriteLine($"  → LEFT SIDE LOCKUP (Steer left but LatAccel:{latAccel:F1})");
                    }
                }
            }
        }
        
        // If no specific wheel detected, default to rear (most common in GT3)
        if (!state.FrontAxleLockup && !state.RearAxleLockup)
        {
            state.RearAxleLockup = true;
            state.LeftRearLocked = true;
            state.RightRearLocked = true;
        }
    }
    
    /// <summary>
    /// Reset detector state (call when session changes or telemetry disconnects)
    /// </summary>
    public static void Reset()
    {
        _decelHistory.Clear();
        _maxDecelThisStop = 0f;
        _isHardBraking = false;
        
        if (EnableDiagnostics)
        {
            Console.WriteLine("[RESET] Deceleration detection reset");
        }
    }
}

/// <summary>
/// Wheel lockup detection result with granular per-wheel information
/// </summary>
public class WheelLockupState
{
    // Individual wheel lockup flags
    public bool LeftFrontLocked { get; set; }
    public bool RightFrontLocked { get; set; }
    public bool LeftRearLocked { get; set; }
    public bool RightRearLocked { get; set; }
    
    // Aggregate flags
    public bool FrontAxleLockup { get; set; }
    public bool RearAxleLockup { get; set; }
    public bool AnyWheelLocked { get; set; }
    
    // Lock attempt flags (wheels would lock without ABS)
    public bool LeftFrontLockAttempted { get; set; }
    public bool RightFrontLockAttempted { get; set; }
    public bool LeftRearLockAttempted { get; set; }
    public bool RightRearLockAttempted { get; set; }
    
    // ABS information
    public bool ABSActive { get; set; }
    public bool ABSPreventingLockup { get; set; }
    
    // Detection metadata
    public LockupDetectionMethod DetectionMethod { get; set; }
    public LockupConfidence Confidence { get; set; }
}

/// <summary>
/// Detection method used to identify lockup
/// </summary>
public enum LockupDetectionMethod
{
    None,
    OdometerBased,      // Wheel rotation rate vs car speed
    ABS,                // ABS system active
    PressureCollapse,   // Brake pressure collapse
    PressureImbalance,  // Individual wheel pressure drop
    DecelerationPlateau,// Deceleration plateau/drop
    TireRumble          // Tire rumble pitch spike
}

/// <summary>
/// Confidence level of lockup detection
/// </summary>
public enum LockupConfidence
{
    None,
    Low,
    Medium,
    High
}
