using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Service for pit stop intelligence and optimization
/// Analyzes damage, estimates service times, and calculates optimal pit strategies
/// </summary>
public class PitIntelligenceService
{
    /// <summary>
    /// Pit service estimate
    /// </summary>
    public class PitServiceEstimate
    {
        public float FuelTime { get; set; } // Seconds to add fuel
        public float TireChangeTime { get; set; } // Seconds to change tires
        public float RepairTime { get; set; } // Seconds for repairs
        public float TotalPitTime { get; set; } // Total estimated pit time
        public bool HasDamage { get; set; }
        public bool NeedsTireChange { get; set; }
        public bool NeedsFuel { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// Damage assessment
    /// </summary>
    public class DamageAssessment
    {
        public bool HasAeroDamage { get; set; }
        public bool HasSuspensionDamage { get; set; }
        public bool HasEngineDamage { get; set; }
        public float EstimatedLapTimeLoss { get; set; } // Seconds per lap
        public string DamageDescription { get; set; } = string.Empty;
        public bool ShouldRepair { get; set; }
        public string RepairRecommendation { get; set; } = string.Empty;
    }

    // Standard pit service times (can be adjusted per car/series)
    private const float BASE_PIT_TIME = 15f; // Base time for entering/exiting pit box
    private const float FUEL_PER_LITER_TIME = 0.5f; // Seconds per liter
    private const float TIRE_CHANGE_TIME = 5f; // Seconds for full tire change
    private const float REPAIR_BASE_TIME = 10f; // Base repair time

    /// <summary>
    /// Estimate pit service time based on requirements
    /// Uses actual SDK repair time (optRepairTime) instead of generic constants
    /// </summary>
    public PitServiceEstimate EstimatePitService(float fuelToAdd, bool changeTires, float optRepairTime)
    {
        var estimate = new PitServiceEstimate
        {
            NeedsFuel = fuelToAdd > 0,
            NeedsTireChange = changeTires,
            HasDamage = optRepairTime > 0
        };

        float totalTime = BASE_PIT_TIME;

        // Fuel time
        if (fuelToAdd > 0)
        {
            estimate.FuelTime = fuelToAdd * FUEL_PER_LITER_TIME;
            totalTime += estimate.FuelTime;
        }

        // Tire change time (happens concurrently with fueling if both needed)
        if (changeTires)
        {
            estimate.TireChangeTime = TIRE_CHANGE_TIME;
            // Tires can be changed during fueling, so only add extra time if longer
            if (estimate.TireChangeTime > estimate.FuelTime)
                totalTime += (estimate.TireChangeTime - estimate.FuelTime);
        }

        // Repair time (use actual SDK value, happens after fuel/tires)
        if (optRepairTime > 0)
        {
            estimate.RepairTime = optRepairTime; // Use SDK's actual repair time
            totalTime += estimate.RepairTime;
        }

        estimate.TotalPitTime = totalTime;

        // Generate recommendation
        if (estimate.HasDamage && estimate.NeedsFuel && estimate.NeedsTireChange)
            estimate.RecommendedAction = "Full service: Fuel + Tires + Repair";
        else if (estimate.NeedsFuel && estimate.NeedsTireChange)
            estimate.RecommendedAction = "Fuel + Tire change";
        else if (estimate.NeedsFuel && estimate.HasDamage)
            estimate.RecommendedAction = "Fuel + Repair";
        else if (estimate.NeedsFuel)
            estimate.RecommendedAction = "Fuel only (quick stop)";
        else if (estimate.NeedsTireChange)
            estimate.RecommendedAction = "Tire change only";
        else if (estimate.HasDamage)
            estimate.RecommendedAction = "Repair only";
        else
            estimate.RecommendedAction = "No service needed";

        return estimate;
    }

    /// <summary>
    /// Assess vehicle damage and recommend repairs
    /// Uses actual SDK pit repair times (PitRepairLeft, PitOptRepairLeft) for accurate damage assessment
    /// </summary>
    public DamageAssessment AssessDamage(TelemetryData data)
    {
        var assessment = new DamageAssessment();

        // Use SDK's actual pit repair times for accurate damage assessment
        // PitRepairLeft = mandatory repairs (must do before continuing)
        // PitOptRepairLeft = optional repairs (can skip but will affect performance)
        
        float mandatoryRepairTime = data.PitRepairLeft;
        float optionalRepairTime = data.PitOptRepairLeft;
        float totalRepairTime = mandatoryRepairTime + optionalRepairTime;

        if (totalRepairTime <= 0)
        {
            // No damage detected
            assessment.DamageDescription = "No damage detected";
            assessment.RepairRecommendation = "Vehicle in good condition";
            return assessment;
        }

        // Damage exists - categorize by repair time
        assessment.HasAeroDamage = optionalRepairTime > 0; // Optional repairs typically aero/bodywork
        assessment.HasSuspensionDamage = mandatoryRepairTime > 0; // Mandatory repairs typically suspension/critical
        assessment.HasEngineDamage = false; // SDK doesn't distinguish engine damage separately
        
        // Estimate lap time loss based on repair time
        // Rule of thumb: ~0.05s per second of repair time (rough approximation)
        // Heavy damage (20s repair) ≈ 1.0s/lap loss
        assessment.EstimatedLapTimeLoss = totalRepairTime * 0.05f;

        // Build damage description
        if (mandatoryRepairTime > 0 && optionalRepairTime > 0)
        {
            assessment.DamageDescription = $"Critical damage: {mandatoryRepairTime:F1}s mandatory + {optionalRepairTime:F1}s optional repair";
        }
        else if (mandatoryRepairTime > 0)
        {
            assessment.DamageDescription = $"Critical damage: {mandatoryRepairTime:F1}s mandatory repair required";
        }
        else
        {
            assessment.DamageDescription = $"Minor damage: {optionalRepairTime:F1}s optional repair available";
        }

        // Recommend repair based on severity
        if (mandatoryRepairTime > 0)
        {
            // Mandatory repairs required
            assessment.ShouldRepair = true;
            assessment.RepairRecommendation = "Critical damage detected. Mandatory repair required before continuing.";
        }
        else if (optionalRepairTime >= 15f)
        {
            // Significant optional damage (15+ seconds)
            assessment.ShouldRepair = true;
            assessment.RepairRecommendation = $"Significant damage ({optionalRepairTime:F1}s repair). Strongly recommend repair at next pit stop.";
        }
        else if (optionalRepairTime >= 5f)
        {
            // Moderate optional damage (5-15 seconds)
            assessment.ShouldRepair = false;
            assessment.RepairRecommendation = $"Moderate damage ({optionalRepairTime:F1}s repair). Consider repair if pitting for other reasons.";
        }
        else
        {
            // Minor optional damage (<5 seconds)
            assessment.ShouldRepair = false;
            assessment.RepairRecommendation = $"Minor damage ({optionalRepairTime:F1}s repair). Likely not worth the time penalty.";
        }

        return assessment;
    }

    /// <summary>
    /// Calculate optimal pit lap considering fuel, tires, and damage
    /// </summary>
    public int CalculateOptimalPitLap(
        int currentLap,
        int totalLaps,
        int fuelOptimalLap,
        float tireLifeRemaining,
        bool hasDamage,
        int position)
    {
        // Start with fuel-based optimal lap
        int optimalLap = fuelOptimalLap;

        // Adjust for tire wear if tires are critical
        if (tireLifeRemaining < 20f) // Less than 20% tire life
        {
            // Pit earlier if tires are degraded
            optimalLap = Math.Min(optimalLap, currentLap + 2);
        }

        // Adjust for damage if significant
        if (hasDamage)
        {
            // If leading, wait until normal pit window
            // If mid-pack, pit slightly earlier to avoid traffic
            if (position > 5)
                optimalLap = Math.Max(currentLap + 1, optimalLap - 2);
        }

        // Ensure we don't pit too early or too late
        optimalLap = Math.Clamp(optimalLap, currentLap + 1, totalLaps - 2);

        return optimalLap;
    }

    /// <summary>
    /// Calculate position loss from pit stop
    /// </summary>
    public int EstimatePositionLoss(float pitTime, float avgLapTime, int carsWithinGap)
    {
        if (avgLapTime <= 0)
            return 0;

        // Calculate how many laps worth of time the pit stop costs
        float lapsEquivalent = pitTime / avgLapTime;

        // Estimate cars that will pass (rough approximation)
        // Each car within 1 lap gap could potentially pass
        int estimatedLoss = Math.Min(carsWithinGap, (int)Math.Ceiling(lapsEquivalent));

        return estimatedLoss;
    }

    /// <summary>
    /// Determine if a pit stop should include optional repairs
    /// </summary>
    public bool ShouldTakeOptionalRepair(
        DamageAssessment damage,
        int remainingLaps,
        int position,
        float repairTime)
    {
        // Don't repair if no damage
        if (!damage.ShouldRepair)
            return false;

        // Always repair if significant damage
        if (damage.EstimatedLapTimeLoss > 0.5f)
            return true;

        // If near end of race and fighting for position, skip repair
        if (remainingLaps < 5 && position <= 3)
            return false;

        // Calculate if repair time will be recovered over remaining laps
        float timeLostFromDamage = damage.EstimatedLapTimeLoss * remainingLaps;
        bool repairWillPayOff = timeLostFromDamage > repairTime;

        return repairWillPayOff;
    }

    /// <summary>
    /// Get pit speed limit warning status
    /// </summary>
    public string GetPitSpeedStatus(float currentSpeed, float pitSpeedLimit)
    {
        if (pitSpeedLimit <= 0)
            return "N/A";

        float speedKmh = currentSpeed * 3.6f; // Convert m/s to km/h
        float limitKmh = pitSpeedLimit * 3.6f;

        if (currentSpeed > pitSpeedLimit + 0.5f)
            return $"⚠️ SPEEDING! {speedKmh:F0} km/h (limit: {limitKmh:F0})";
        else if (currentSpeed > pitSpeedLimit)
            return $"⚠️ Over limit: {speedKmh:F0} km/h (limit: {limitKmh:F0})";
        else
            return $"✓ {speedKmh:F0} km/h (limit: {limitKmh:F0})";
    }
}
