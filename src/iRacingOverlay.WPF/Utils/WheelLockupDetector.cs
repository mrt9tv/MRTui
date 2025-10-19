using iRacingOverlay.Core.Models;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// DECELERATION-BASED wheel lock-up detection system for iRacing telemetry
/// Uses longitudinal acceleration (deceleration) inefficiency to detect wheel lockup
/// PHYSICS PRINCIPLE: Locked wheels have LESS grip than rolling wheels
/// → Lockup causes deceleration to DROP or PLATEAU despite increased brake input
/// Works universally - no sensors required, pure physics-based detection
/// 
/// DEBUG LOGGING: All Console.WriteLine output is automatically written to:
/// - Console window (real-time monitoring)
/// - wheel_lockup_debug.log file (persistent logging)
/// See App.xaml.cs MultiTextWriter for implementation details
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
    /// Default 0.06 = 6% deceleration drop (HYPER AGGRESSIVE - 15% more sensitive than before)
    /// Example: Was decelerating at 20 m/s², now only 18.8 m/s² despite more brake → LOCKED
    /// </summary>
    public static float DecelDropThreshold { get; set; } = 0.06f;
    
    /// <summary>
    /// Brake input increase required to detect inefficiency
    /// Must be braking harder by at least this much to compare efficiency
    /// Default 0.02 = 2% more brake input (VERY AGGRESSIVE - sensitive to small changes)
    /// </summary>
    public static float BrakeIncreaseThreshold { get; set; } = 0.02f;
    
    // ===== BRAKE PRESSURE DETECTION THRESHOLDS =====
    
    /// <summary>
    /// Minimum brake line pressure to consider (bar)
    /// Below this, pressure readings may be unreliable
    /// LOWERED: 10.0 → 5.0 bar to catch light trail braking (turning + braking)
    /// </summary>
    public static float MinBrakePressure { get; set; } = 5.0f;
    
    /// <summary>
    /// Pressure drop threshold indicating wheel lockup (bar)
    /// When one wheel's pressure drops this much below average → locked
    /// ADAPTIVE: Uses PERCENTAGE-based threshold for light braking, absolute for hard braking
    /// Light braking (20%): 15% drop = ~0.9 bar at 6 bar avg
    /// Hard braking (80%): 15% drop = ~3.0 bar at 20 bar avg
    /// Minimum absolute floor to prevent false positives from sensor noise
    /// </summary>
    public static float PressureDropThresholdPercentage { get; set; } = 0.15f; // 15% drop
    
    /// <summary>
    /// Minimum absolute pressure drop (bar) - safety floor to prevent sensor noise false positives
    /// </summary>
    public static float MinPressureDropThreshold { get; set; } = 0.8f;
    
    /// <summary>
    /// Minimum speed for pressure-based detection (m/s)
    /// Below this speed, pressure detection is disabled (low-speed braking is less critical)
    /// LOWERED: 20.0 → 10.0 m/s (36 km/h / ~22 mph) for slow corner entry lockups
    /// </summary>
    public static float MinPressureDetectionSpeed { get; set; } = 10.0f;
    
    // ===== BRAKE PRESSURE DETECTION METHOD =====
    
    /// <summary>
    /// Detect wheel lockup via brake line pressure imbalance
    /// PHYSICS: Locked wheel → sliding tire → friction drops → brake pressure drops
    /// This is PREDICTIVE detection (detects pressure drop BEFORE deceleration inefficiency)
    /// </summary>
    private static WheelLockupState DetectPressureImbalance(TelemetryData data)
    {
        var state = new WheelLockupState();
        
        // Early exit: Only check at speed during ANY braking (even light trail braking)
        // CRITICAL FIX: Lowered from 50% to 15% brake to catch trail braking into corners!
        // Trail braking = 20-40% brake while turning = MOST COMMON single-wheel lockup scenario
        if (data.Brake < 0.15f || data.Speed < MinPressureDetectionSpeed)
        {
            if (EnableDiagnostics && data.Brake > 0.12f && data.Speed > MinPressureDetectionSpeed * 0.85f)
            {
                Console.WriteLine($"[PRESSURE] Early exit: Brake:{data.Brake:P0} (min:15%) Speed:{data.Speed:F1}m/s (min:{MinPressureDetectionSpeed:F1}m/s)");
            }
            return state;
        }
        
        // Calculate average pressures per axle
        float avgFrontPress = (data.LFbrakeLinePress + data.RFbrakeLinePress) / 2f;
        float avgRearPress = (data.LRbrakeLinePress + data.RRbrakeLinePress) / 2f;
        
        // ADAPTIVE THRESHOLD: Use percentage of average pressure, with minimum absolute floor
        // Light braking (6 bar avg): 15% = 0.9 bar (uses 0.9 bar)
        // Hard braking (20 bar avg): 15% = 3.0 bar (uses 3.0 bar)
        // This adapts to brake intensity while preventing sensor noise false positives
        float frontPressureDropThreshold = Math.Max(MinPressureDropThreshold, avgFrontPress * PressureDropThresholdPercentage);
        float rearPressureDropThreshold = Math.Max(MinPressureDropThreshold, avgRearPress * PressureDropThresholdPercentage);
        
        // DEBUG: Log pressure readings when braking (even light braking for trail braking scenarios)
        // CRITICAL: Lowered from 50% to 15% to monitor trail braking into corners (matches detection threshold!)
        if (EnableDiagnostics && data.Brake > 0.15f)
        {
            // Calculate pressure drops for diagnostic purposes
            float lfDrop = avgFrontPress - data.LFbrakeLinePress;
            float rfDrop = avgFrontPress - data.RFbrakeLinePress;
            float lrDrop = avgRearPress - data.LRbrakeLinePress;
            float rrDrop = avgRearPress - data.RRbrakeLinePress;
            
            Console.WriteLine($"[PRESSURE DEBUG] Brake:{data.Brake:P0} Speed:{data.Speed:F1}m/s " +
                            $"LF:{data.LFbrakeLinePress:F1}(-{lfDrop:F1}) RF:{data.RFbrakeLinePress:F1}(-{rfDrop:F1}) (AvgF:{avgFrontPress:F1}) " +
                            $"LR:{data.LRbrakeLinePress:F1}(-{lrDrop:F1}) RR:{data.RRbrakeLinePress:F1}(-{rrDrop:F1}) (AvgR:{avgRearPress:F1}) " +
                            $"Thresholds: F={frontPressureDropThreshold:F1}bar R={rearPressureDropThreshold:F1}bar");
            
            // Log near-miss cases (within 0.3 bar of adaptive threshold)
            float maxFrontDrop = Math.Max(lfDrop, rfDrop);
            float maxRearDrop = Math.Max(lrDrop, rrDrop);
            if ((maxFrontDrop > frontPressureDropThreshold - 0.3f && maxFrontDrop < frontPressureDropThreshold) ||
                (maxRearDrop > rearPressureDropThreshold - 0.3f && maxRearDrop < rearPressureDropThreshold))
            {
                Console.WriteLine($"  ⚠️ NEAR THRESHOLD! Max drops: Front={maxFrontDrop:F1} Rear={maxRearDrop:F1} (Thresholds: F={frontPressureDropThreshold:F1} R={rearPressureDropThreshold:F1})");
            }
        }
        
        // Detect individual wheel lockups via pressure drop
        // ADAPTIVE: Use percentage-based threshold that scales with brake intensity
        // PHYSICS: Locked wheel drops BELOW MinBrakePressure, but we still want to detect it!
        bool lfLocked = avgFrontPress > MinBrakePressure && 
                       (avgFrontPress - data.LFbrakeLinePress) > frontPressureDropThreshold;
        
        bool rfLocked = avgFrontPress > MinBrakePressure && 
                       (avgFrontPress - data.RFbrakeLinePress) > frontPressureDropThreshold;
        
        bool lrLocked = avgRearPress > MinBrakePressure && 
                       (avgRearPress - data.LRbrakeLinePress) > rearPressureDropThreshold;
        
        bool rrLocked = avgRearPress > MinBrakePressure && 
                       (avgRearPress - data.RRbrakeLinePress) > rearPressureDropThreshold;
        
        // Populate state if any wheel locked
        if (lfLocked || rfLocked || lrLocked || rrLocked)
        {
            state.AnyWheelLocked = true;
            state.LeftFrontLocked = lfLocked;
            state.RightFrontLocked = rfLocked;
            state.LeftRearLocked = lrLocked;
            state.RightRearLocked = rrLocked;
            
            // Set axle flags
            state.FrontAxleLockup = lfLocked || rfLocked;
            state.RearAxleLockup = lrLocked || rrLocked;
            
            // Metadata
            state.DetectionMethod = LockupDetectionMethod.PressureImbalance;
            state.Confidence = LockupConfidence.High;
            
            // DEBUG: Log detailed pressure lockup info
            if (EnableDiagnostics)
            {
                string wheels = $"{(lfLocked ? "LF " : "")}{(rfLocked ? "RF " : "")}{(lrLocked ? "LR " : "")}{(rrLocked ? "RR " : "")}";
                Console.WriteLine($"🔴 PRESSURE LOCKUP! {wheels}| " +
                                $"Front: LF:{data.LFbrakeLinePress:F1} RF:{data.RFbrakeLinePress:F1} (Avg:{avgFrontPress:F1} Threshold:{frontPressureDropThreshold:F1}) | " +
                                $"Rear: LR:{data.LRbrakeLinePress:F1} RR:{data.RRbrakeLinePress:F1} (Avg:{avgRearPress:F1} Threshold:{rearPressureDropThreshold:F1})");
            }
        }
        
        return state;
    }
    
    // ===== PUBLIC DETECTION METHOD =====
    
    /// <summary>
    /// Detect wheel lockup using HYBRID detection: deceleration inefficiency + brake pressure imbalance
    /// </summary>
    public static WheelLockupState DetectLockup(TelemetryData data)
    {
        var state = new WheelLockupState();
        
        // ===== BRAKE PRESSURE DETECTION (Priority #0 - Most Accurate) =====
        // Check brake pressure FIRST before deceleration analysis
        // Pressure drop is PREDICTIVE (detects cause) vs decel drop is REACTIVE (measures effect)
        var pressureState = DetectPressureImbalance(data);
        if (pressureState.AnyWheelLocked)
        {
            // Pressure detection found lockup - return immediately for fastest response
            return pressureState;
        }
        
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
        
        // Mark as hard braking (VERY AGGRESSIVE - even light trail braking)
        if (!_isHardBraking && data.Brake > 0.12f)
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
            float avgLatAccel = Math.Abs((recentSamples[0].LatAccel + recentSamples[1].LatAccel) * 0.5f);
            
            // FRICTION CIRCLE COMPENSATION: Adjust expected decel for cornering
            float maxStraightLineDecel = 18.0f;
            float maxDecelWithLatLoad = avgLatAccel > 0.5f 
                ? (float)Math.Sqrt(Math.Max(0, maxStraightLineDecel * maxStraightLineDecel - avgLatAccel * avgLatAccel))
                : maxStraightLineDecel;
            
            // REDUCED COMPENSATION for slight turns (single-wheel lockups more common at low lat G)
            // At low lateral G, single-wheel lockup causes proportional decel loss without full friction circle effect
            // Blend factor: 0-5 m/s² = 50% compensation, 5-10 m/s² = 100% compensation
            float compensationBlend = Math.Min(1.0f, avgLatAccel / 5.0f);
            float blendedMaxDecel = maxStraightLineDecel + (maxDecelWithLatLoad - maxStraightLineDecel) * compensationBlend;
            
            // Check if decel efficiency is back to normal (using cornering-adjusted baseline)
            float expectedDecel = avgBrake * blendedMaxDecel;
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
                    Console.WriteLine($"✅ UNLOCK! ({reason}) Efficiency:{efficiency:P0} Decel:{avgDecel:F1}m/s² Brake:{avgBrake:P0} Lat:{avgLatAccel:F1}m/s²");
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
            
            // SPEED-DEPENDENT THRESHOLD: MORE sensitive at low speeds
            // Below 60 km/h (~16.7 m/s), lockups are more dangerous - detect earlier!
            float speedFactor = data.Speed < 16.7f ? 0.85f : 1.0f; // 15% MORE sensitive below 60 km/h
            float adjustedDropThreshold = DecelDropThreshold * speedFactor;
            
            // LOCKUP CONDITION: Brake input increased significantly but deceleration dropped
            if (brakeIncrease > BrakeIncreaseThreshold && decelRatio < (1.0f - adjustedDropThreshold))
            {
                state.AnyWheelLocked = true;
                state.DetectionMethod = LockupDetectionMethod.DecelerationPlateau;
                state.Confidence = LockupConfidence.High;
                _wasLocked = true;
                _unlockConfirmFrames = 0;
                
                // SIMPLIFIED: Don't try to determine which wheel - just flag lockup exists
                // Pressure-based detection handles per-wheel identification
                state.FrontAxleLockup = true; // Default assumption (most common)
                state.LeftFrontLocked = true;
                state.RightFrontLocked = true;
                
                if (EnableDiagnostics)
                {
                    Console.WriteLine($"🔴 LOCKUP! Brake +{brakeIncrease:F2} decel {oldAvgDecel:F1}→{newAvgDecel:F1} ({decelRatio:F2})");
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
            // FRICTION CIRCLE COMPENSATION: Adjust expected decel for lateral load during cornering
            // Physics: Total tire grip is limited - lateral G reduces available longitudinal grip
            // Formula: maxLongitudinal = sqrt(totalGrip² - lateral²)
            float maxStraightLineDecel = 18.0f; // GT3 baseline (straight-line braking)
            float absLatAccel = Math.Abs(latAccel); // Absolute lateral G (direction doesn't matter)
            
            // Friction circle: reduce expected decel based on lateral load
            // If lateral = 10 m/s² (1G), max longitudinal ≈ sqrt(18² - 10²) ≈ 14.8 m/s²
            float maxDecelWithLatLoad = absLatAccel > 0.5f 
                ? (float)Math.Sqrt(Math.Max(0, maxStraightLineDecel * maxStraightLineDecel - absLatAccel * absLatAccel))
                : maxStraightLineDecel;
            
            // CRITICAL FIX: PARTIAL compensation for slight turns (single-wheel lockups!)
            // At low lateral G, single-wheel lockup causes decel loss WITHOUT full friction circle effect
            // SIMPLIFIED: Reduce compensation at low brake % (trail braking is sensitive zone)
            float brakeFactor = Math.Min(1.0f, data.Brake / 0.20f); // 0-100% from 0-20% brake
            float baseBlend = Math.Min(1.0f, absLatAccel / 5.0f);
            float compensationBlend = baseBlend * brakeFactor;
            float blendedMaxDecel = maxStraightLineDecel + (maxDecelWithLatLoad - maxStraightLineDecel) * compensationBlend;
            
            // Calculate efficiency using BLENDED cornering-adjusted baseline
            float expectedDecelForBrake = data.Brake * blendedMaxDecel;
            float decelEfficiency = expectedDecelForBrake > 1.0f ? (decel / expectedDecelForBrake) : 1.0f;
            
            // THREE-TIER THRESHOLDS: Trail braking, Normal braking, Heavy braking
            // 15% MORE SENSITIVE than previous version (thresholds × 0.85)
            float sustainedEfficiencyThreshold = data.Brake < 0.25f ? 0.51f :   // Trail braking (5-25%): 51% (hyper sensitive)
                                                  data.Brake > 0.70f ? 0.43f :   // Heavy braking (70-100%): 43% (very aggressive)
                                                  0.30f;                         // Normal braking (25-70%): 30% (aggressive)
            
            // Detect lockup across full brake range
            bool lockupDetected = decelEfficiency < sustainedEfficiencyThreshold;
            
            if (lockupDetected)
            {
                state.AnyWheelLocked = true;
                state.DetectionMethod = LockupDetectionMethod.DecelerationPlateau;
                state.Confidence = LockupConfidence.High;
                _wasLocked = true;
                _unlockConfirmFrames = 0;
                
                // SIMPLIFIED: Don't try to determine which wheel - just flag lockup exists
                state.FrontAxleLockup = true;
                state.LeftFrontLocked = true;
                state.RightFrontLocked = true;
                
                if (EnableDiagnostics)
                {
                    Console.WriteLine($"🔴 SUSTAINED! Brake:{data.Brake:P0} Decel:{decel:F1} (Exp:{expectedDecelForBrake:F1} Eff:{decelEfficiency:P0} Thresh:{sustainedEfficiencyThreshold:P0}) Lat:{Math.Abs(latAccel):F1}m/s² Speed:{data.Speed:F1}m/s");
                }
                
                return state;
            }
        }
        
        // ===== DECELERATION PLATEAU DETECTION (High Brake Alternative) =====
        // If braking but decel is significantly below maximum achieved → LOCKUP
        // CRITICAL: More aggressive at high brake % and high speeds!
        // Performance: Simple ratio check, no array operations
        if (_isHardBraking && data.Brake > 0.15f && _maxDecelThisStop > 6.0f)
        {
            float decelRatioVsMax = decel / _maxDecelThisStop;
            
            // SPEED + BRAKE DEPENDENT: Stricter at LOW speeds and HIGH brake %
            // 15% MORE SENSITIVE than previous version (base threshold × 0.85)
            float speedFactor = data.Speed < 16.7f ? 0.85f : 1.0f; // MORE sensitive below 60 km/h
            float brakeFactor = data.Brake > 0.70f ? 1.08f : 1.0f; // 8% stricter at heavy braking (70-100%)
            float adjustedPlateauThreshold = 0.62f * speedFactor * brakeFactor; // Base: 0.62 (was 0.73)
            
            // If current decel is < threshold of max decel achieved → wheels locked
            if (decelRatioVsMax < adjustedPlateauThreshold)
            {
                state.AnyWheelLocked = true;
                state.DetectionMethod = LockupDetectionMethod.DecelerationPlateau;
                state.Confidence = LockupConfidence.Medium;
                _wasLocked = true;
                _unlockConfirmFrames = 0;
                
                // SIMPLIFIED: Don't try to determine which wheel - just flag lockup exists
                state.FrontAxleLockup = true;
                state.LeftFrontLocked = true;
                state.RightFrontLocked = true;
                
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
        // CRITICAL: Lowered threshold to 12% to catch light trail braking lockups!
        // Performance: Use current decel directly instead of averaging
        if (data.Brake > 0.12f)
        {
            // FRICTION CIRCLE COMPENSATION: Adjust expected decel for lateral load
            float maxStraightLineDecel = 18.0f; // GT3 baseline
            float absLatAccel = Math.Abs(latAccel);
            
            // Reduce expected decel based on lateral load (cornering physics)
            float maxDecelWithLatLoad = absLatAccel > 0.5f 
                ? (float)Math.Sqrt(Math.Max(0, maxStraightLineDecel * maxStraightLineDecel - absLatAccel * absLatAccel))
                : maxStraightLineDecel;
            
            // CRITICAL FIX: PARTIAL compensation for slight turns
            // SIMPLIFIED: Less compensation at low brake % (trail braking zone)
            float brakeFactor = Math.Min(1.0f, data.Brake / 0.20f);
            float baseBlend = Math.Min(1.0f, absLatAccel / 5.0f);
            float compensationBlend = baseBlend * brakeFactor;
            float blendedMaxDecel = maxStraightLineDecel + (maxDecelWithLatLoad - maxStraightLineDecel) * compensationBlend;
            
            // Expected deceleration based on brake input (blended cornering-adjusted)
            float expectedDecel = data.Brake * blendedMaxDecel;
            
            // THREE-TIER THRESHOLDS: Trail braking, Normal braking, Heavy braking
            // 15% MORE SENSITIVE than previous version (thresholds × 0.85)
            float adjustedEfficiencyThreshold = data.Brake < 0.25f ? 0.60f :  // Trail braking (5-25%): 60% (hyper sensitive)
                                                 data.Brake > 0.70f ? 0.55f :  // Heavy braking (70-100%): 55% (very aggressive)
                                                 0.47f;                         // Normal braking (25-70%): 47% (aggressive)
            
            // If actual decel is much lower than expected → LOCKUP (AGGRESSIVE for speed)
            if (expectedDecel > 6.0f && decel < expectedDecel * adjustedEfficiencyThreshold)
            {
                state.AnyWheelLocked = true;
                state.DetectionMethod = LockupDetectionMethod.DecelerationPlateau;
                state.Confidence = LockupConfidence.Medium;
                _wasLocked = true;
                _unlockConfirmFrames = 0;
                
                // SIMPLIFIED: Don't try to determine which wheel - just flag lockup exists
                state.FrontAxleLockup = true;
                state.LeftFrontLocked = true;
                state.RightFrontLocked = true;
                
                if (EnableDiagnostics)
                {
                    Console.WriteLine($"🔴 INSTANT! Brake:{data.Brake:P0} Expected:{expectedDecel:F1} Actual:{decel:F1} ({decel/expectedDecel:P0} Thresh:{adjustedEfficiencyThreshold:P0}) Lat:{Math.Abs(latAccel):F1}m/s² Speed:{data.Speed:F1}m/s");
                }
                
                return state;
            }
        }
        
        return state;
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
