using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Service for calculating pit strategy and timing
/// Handles optimal pit lap, fuel to add, position predictions, and multi-stop analysis
/// Phase 3: Extracted from FuelCalculatorService
/// </summary>
public class PitStrategyService
{
    private readonly SessionPersistenceService? _persistenceService;
    
    public PitStrategyService(SessionPersistenceService? persistenceService = null)
    {
        _persistenceService = persistenceService;
    }
    
    /// <summary>
    /// Calculate comprehensive pit strategy
    /// </summary>
    public PitStrategy Calculate(
        TelemetryData telemetry,
        FuelData currentData,
        FuelAverages averages,
        List<FuelLapHistory> lapHistory)
    {
        var strategy = new PitStrategy();
        
        // Basic strategy calculations
        CalculateBasicStrategy(telemetry, currentData, averages, strategy);
        
        // Optimal pit lap with multi-factor analysis
        if (!currentData.CanFinishWithoutStop && currentData.RaceLapsRemaining > 0)
        {
            CalculateOptimalPitLap(telemetry, currentData, averages, lapHistory, strategy);
            CalculatePitExitPosition(telemetry, currentData, strategy);
            CalculateMultiStopStrategy(telemetry, currentData, averages, strategy);
            CalculatePartialRefuelOptimization(currentData, averages, strategy);
        }
        
        return strategy;
    }
    
    /// <summary>
    /// Calculate basic pit strategy (can finish, fuel to add)
    /// </summary>
    private void CalculateBasicStrategy(
        TelemetryData telemetry,
        FuelData currentData,
        FuelAverages averages,
        PitStrategy strategy)
    {
        strategy.CanFinishWithoutStop = currentData.CanFinishWithoutStop;
        strategy.FuelToAddAtPit = currentData.FuelToAddAtPit;
        strategy.RacePosition = currentData.RacePosition;
        strategy.TotalCars = currentData.TotalCars;
    }
    
    /// <summary>
    /// Calculate optimal pit lap using multi-factor analysis
    /// Factors: fuel criticality, track position, yellow flag probability
    /// </summary>
    private void CalculateOptimalPitLap(
        TelemetryData telemetry,
        FuelData currentData,
        FuelAverages averages,
        List<FuelLapHistory> lapHistory,
        PitStrategy strategy)
    {
        if (currentData.RaceLapsRemaining <= 0 || currentData.CanFinishWithoutStop)
        {
            strategy.OptimalPitLap = 0;
            strategy.OptimalPitReason = null;
            return;
        }
        
        int currentLap = telemetry.LapsCompleted;
        float lapsOnCurrentFuel = currentData.LapsRemaining;
        int raceLapsRemaining = currentData.RaceLapsRemaining;
        
        // Fuel criticality score (0-100)
        float fuelCriticality = CalculateFuelCriticality(lapsOnCurrentFuel);
        strategy.FuelCriticalityScore = fuelCriticality;
        
        // Track position cost (dynamic based on field percentile)
        float positionCost = CalculateTrackPositionCost(telemetry, strategy);
        strategy.TrackPositionCost = positionCost;
        
        // Yellow flag prediction
        var (yellowExpected, lapsUntilYellow, yellowProb) = PredictYellowFlag(currentData, lapHistory);
        strategy.YellowFlagProbability = yellowProb;
        strategy.EstimatedLapsUntilYellow = lapsUntilYellow;
        
        // Calculate pit windows
        CalculatePitWindows(currentData, averages, lapsOnCurrentFuel, raceLapsRemaining, strategy);
        
        // Determine optimal pit lap
        int optimalPitLap = DetermineOptimalPitLap(
            telemetry,
            currentData,
            currentLap,
            lapsOnCurrentFuel,
            fuelCriticality,
            positionCost,
            yellowExpected,
            lapsUntilYellow,
            strategy);
        
        strategy.OptimalPitLap = optimalPitLap;
    }
    
    private float CalculateFuelCriticality(float lapsOnCurrentFuel)
    {
        if (lapsOnCurrentFuel < 1.0f)
            return 100f; // CRITICAL
        else if (lapsOnCurrentFuel < 2.0f)
            return 80f + (2f - lapsOnCurrentFuel) * 20f; // 80-100: URGENT
        else if (lapsOnCurrentFuel < 5.0f)
            return 50f + (5f - lapsOnCurrentFuel) * 10f; // 50-80: MODERATE
        else if (lapsOnCurrentFuel < 10.0f)
            return 20f + (10f - lapsOnCurrentFuel) * 6f; // 20-50: COMFORTABLE
        else
            return Math.Max(0f, 20f - (lapsOnCurrentFuel - 10f)); // 0-20: PLENTY
    }
    
    private float CalculateTrackPositionCost(TelemetryData telemetry, PitStrategy strategy)
    {
        int totalCars = 0;
        int playerPosition = 0;
        
        if (telemetry.CarIdxPosition != null && telemetry.CarIdxPosition.Length > 0)
        {
            totalCars = telemetry.CarIdxPosition.Count(p => p > 0);
            if (telemetry.PlayerCarIdx >= 0 && telemetry.PlayerCarIdx < telemetry.CarIdxPosition.Length)
            {
                playerPosition = telemetry.CarIdxPosition[telemetry.PlayerCarIdx];
            }
        }
        
        strategy.RacePosition = playerPosition;
        strategy.TotalCars = totalCars;
        
        if (totalCars > 1 && playerPosition > 0)
        {
            float percentile = (float)playerPosition / totalCars;
            
            if (percentile <= 0.10f) return 25f;      // Top 10%
            else if (percentile <= 0.25f) return 20f; // Top 25%
            else if (percentile <= 0.50f) return 15f; // Top 50%
            else if (percentile <= 0.75f) return 10f; // 50-75%
            else return 5f;                            // Bottom 25%
        }
        
        return 15f; // Default
    }
    
    private (bool expected, int lapsUntil, float probability) PredictYellowFlag(
        FuelData currentData,
        List<FuelLapHistory> lapHistory)
    {
        if (currentData.YellowFlagLapCount > 0 && lapHistory.Count > 10)
        {
            int totalLaps = lapHistory.Count;
            float yellowFrequency = (float)totalLaps / currentData.YellowFlagLapCount;
            
            // Find laps since last yellow
            int lapsSinceYellow = 0;
            for (int i = lapHistory.Count - 1; i >= 0; i--)
            {
                if (lapHistory[i].FlagStatus == LapFlagStatus.Yellow)
                    break;
                lapsSinceYellow++;
            }
            
            int lapsUntilYellow = (int)Math.Max(0, yellowFrequency - lapsSinceYellow);
            float probability = Math.Min(100f, (lapsSinceYellow / yellowFrequency) * 100f);
            bool expected = probability > 50f;
            
            return (expected, lapsUntilYellow, probability);
        }
        
        return (false, 0, 0f);
    }
    
    private void CalculatePitWindows(
        FuelData currentData,
        FuelAverages averages,
        float lapsOnCurrentFuel,
        int raceLapsRemaining,
        PitStrategy strategy)
    {
        float avgFuel = averages.L5 > 0 ? averages.L5 : currentData.AvgFuelPerLap;
        float tankCapacity = currentData.TankCapacity;
        
        // Earliest: When we need buffer fuel (account for buffer)
        int earliest = Math.Max(1, (int)Math.Floor(lapsOnCurrentFuel - currentData.FuelBufferLaps - 1));
        
        // Latest: Must pit before running out
        int latest = Math.Max(1, (int)Math.Floor(lapsOnCurrentFuel - 0.5f));
        
        // Window for optimal pit stop
        strategy.EarliestPitLap = earliest;
        strategy.LatestPitLap = latest;
        strategy.PitWindowStart = earliest;
        strategy.PitWindowEnd = latest;
        strategy.PitWindowReason = $"Pit between laps {earliest}-{latest} (fuel window)";
    }
    
    private int DetermineOptimalPitLap(
        TelemetryData telemetry,
        FuelData currentData,
        int currentLap,
        float lapsOnCurrentFuel,
        float fuelCriticality,
        float positionCost,
        bool yellowExpected,
        int lapsUntilYellow,
        PitStrategy strategy)
    {
        int optimalPitLap = 0;
        
        // Priority 1: Green held or 1 lap to green (pit NOW)
        var flagStatus = DetectFlagStatus(telemetry.SessionFlags);
        if ((flagStatus == LapFlagStatus.OneLapToGreen || flagStatus == LapFlagStatus.GreenHeld) 
            && lapsOnCurrentFuel > 2f)
        {
            optimalPitLap = currentLap + 1;
            strategy.OptimalPitReason = "Green flag imminent - pit now";
            return optimalPitLap;
        }
        
        // Priority 2: Critical fuel (< 1 lap)
        if (fuelCriticality > 95f)
        {
            optimalPitLap = currentLap + 1;
            strategy.OptimalPitReason = $"CRITICAL FUEL: {lapsOnCurrentFuel:F1} laps remaining";
            return optimalPitLap;
        }
        
        // Priority 3: Yellow flag NOW (free pit stop)
        if (currentData.IsUnderYellow && lapsOnCurrentFuel < 10f)
        {
            optimalPitLap = currentLap + 1;
            strategy.OptimalPitReason = "Yellow flag - free pit stop opportunity";
            return optimalPitLap;
        }
        
        // Priority 4: Yellow expected soon + enough fuel
        if (yellowExpected && lapsOnCurrentFuel > 5f && lapsUntilYellow <= 5)
        {
            optimalPitLap = currentLap + lapsUntilYellow;
            strategy.OptimalPitReason = $"Yellow expected in ~{lapsUntilYellow} laps ({strategy.YellowFlagProbability:F0}% probability)";
            return optimalPitLap;
        }
        
        // Priority 5: Low position cost (undercut opportunity)
        if (positionCost <= 10f && strategy.PitWindowStart <= strategy.LatestPitLap - 3)
        {
            optimalPitLap = currentLap + strategy.PitWindowStart;
            strategy.OptimalPitReason = $"Undercut opportunity (low position cost: {positionCost:F0}s)";
            return optimalPitLap;
        }
        
        // Priority 6: High position cost (preserve position)
        if (positionCost >= 20f)
        {
            optimalPitLap = currentLap + strategy.LatestPitLap;
            strategy.OptimalPitReason = $"Preserve position (high cost: {positionCost:F0}s) - pit late";
            return optimalPitLap;
        }
        
        // Default: Mid-window balance
        optimalPitLap = currentLap + (strategy.PitWindowStart + strategy.LatestPitLap) / 2;
        strategy.OptimalPitReason = $"Balanced strategy (criticality: {fuelCriticality:F0}/100)";
        return optimalPitLap;
    }
    
    private LapFlagStatus DetectFlagStatus(uint sessionFlags)
    {
        const uint OneLapToGreen = 0x00000800;
        const uint GreenHeld = 0x00000400;
        const uint Yellow = 0x00000002;
        
        if ((sessionFlags & OneLapToGreen) != 0) return LapFlagStatus.OneLapToGreen;
        if ((sessionFlags & GreenHeld) != 0) return LapFlagStatus.GreenHeld;
        if ((sessionFlags & Yellow) != 0) return LapFlagStatus.Yellow;
        
        return LapFlagStatus.Green;
    }
    
    /// <summary>
    /// Calculate projected position after pit stop with class filtering
    /// </summary>
    private void CalculatePitExitPosition(
        TelemetryData telemetry,
        FuelData currentData,
        PitStrategy strategy)
    {
        strategy.PitExitPositionValid = false;
        
        if (telemetry.CarIdxPosition == null || telemetry.CarIdxF2Time == null ||
            telemetry.CarIdxClass == null || telemetry.CarIdxToCarNumber == null)
        {
            return;
        }
        
        var sessionStats = _persistenceService?.GetStatistics(telemetry.TrackName, telemetry.PlayerCarClass);
        float pitStopTime = sessionStats?.AverageTotalPitTime ?? 30f;
        
        int playerCarIdx = telemetry.PlayerCarIdx;
        int playerClass = telemetry.PlayerCarClass;
        int playerPosition = strategy.RacePosition;
        
        if (playerPosition <= 0 || playerCarIdx < 0 || pitStopTime <= 0)
            return;
        
        float playerGapToLeader = telemetry.CarIdxF2Time[playerCarIdx];
        float playerProjectedTime = playerGapToLeader + pitStopTime;
        
        // Find cars in player's class
        var classCarData = new List<(int carIdx, float gapToLeader, string carNumber)>();
        
        for (int i = 0; i < telemetry.CarIdxPosition.Length; i++)
        {
            if (telemetry.CarIdxPosition[i] <= 0 || i == playerCarIdx || telemetry.CarIdxClass[i] != playerClass)
                continue;
            
            float gap = telemetry.CarIdxF2Time[i];
            string carNumber = telemetry.CarIdxToCarNumber.TryGetValue(i, out var num) ? num : $"Car{i}";
            classCarData.Add((i, gap, carNumber));
        }
        
        classCarData = classCarData.OrderBy(c => c.gapToLeader).ToList();
        
        int projectedPosition = 1;
        int carAheadIdx = -1;
        int carBehindIdx = -1;
        
        foreach (var car in classCarData)
        {
            if (car.gapToLeader < playerProjectedTime)
            {
                projectedPosition++;
                carAheadIdx = car.carIdx;
            }
            else
            {
                if (carBehindIdx == -1)
                    carBehindIdx = car.carIdx;
            }
        }
        
        strategy.PitExitPosition = projectedPosition;
        strategy.PitExitPositionValid = true;
        
        // Build gap description
        var gapParts = new List<string>();
        if (carAheadIdx >= 0)
        {
            float gap = playerProjectedTime - telemetry.CarIdxF2Time[carAheadIdx];
            string num = telemetry.CarIdxToCarNumber.TryGetValue(carAheadIdx, out var n) ? n : $"{carAheadIdx}";
            gapParts.Add($"{gap:F0}s to #{num}");
        }
        
        if (carBehindIdx >= 0)
        {
            float gap = telemetry.CarIdxF2Time[carBehindIdx] - playerProjectedTime;
            string num = telemetry.CarIdxToCarNumber.TryGetValue(carBehindIdx, out var n) ? n : $"{carBehindIdx}";
            gapParts.Add($"{gap:F0}s from #{num}");
        }
        
        strategy.PitExitGapDescription = gapParts.Count > 0 ? string.Join(", ", gapParts) : null;
    }
    
    /// <summary>
    /// Calculate multi-stop strategy comparison (1-stop, 2-stop, 3-stop)
    /// </summary>
    private void CalculateMultiStopStrategy(
        TelemetryData telemetry,
        FuelData currentData,
        FuelAverages averages,
        PitStrategy strategy)
    {
        if (currentData.RaceLapsRemaining <= 0 || currentData.AvgFuelPerLap <= 0)
        {
            strategy.MultiStopRecommendation = "No pit stops needed";
            return;
        }
        
        float avgFuel = currentData.AvgFuelPerLap;
        float avgLapTime = currentData.AverageLapTime > 0 ? currentData.AverageLapTime : 90f;
        int raceLapsRemaining = currentData.RaceLapsRemaining;
        float tankCapacity = currentData.TankCapacity;
        float maxStintLaps = tankCapacity / avgFuel;
        
        float fuelOnlyStop = currentData.FuelOnlyStopTime > 0 ? currentData.FuelOnlyStopTime : 30f;
        
        // 1-stop
        if (raceLapsRemaining <= maxStintLaps * 2)
        {
            strategy.OneStopTotalTime = (raceLapsRemaining * avgLapTime) + fuelOnlyStop;
        }
        else
        {
            strategy.OneStopTotalTime = float.MaxValue;
        }
        
        // 2-stop
        if (raceLapsRemaining <= maxStintLaps * 3)
        {
            strategy.TwoStopTotalTime = (raceLapsRemaining * avgLapTime) + (fuelOnlyStop * 2);
        }
        else
        {
            strategy.TwoStopTotalTime = float.MaxValue;
        }
        
        // 3-stop
        if (raceLapsRemaining > maxStintLaps * 3)
        {
            strategy.ThreeStopTotalTime = (raceLapsRemaining * avgLapTime) + (fuelOnlyStop * 3);
        }
        else
        {
            strategy.ThreeStopTotalTime = float.MaxValue;
        }
        
        // Determine best strategy
        float[] times = { strategy.OneStopTotalTime, strategy.TwoStopTotalTime, strategy.ThreeStopTotalTime };
        float bestTime = times.Min();
        
        if (bestTime == float.MaxValue)
        {
            strategy.MultiStopRecommendation = "Can finish without pit stop";
        }
        else if (bestTime == strategy.OneStopTotalTime)
        {
            strategy.MultiStopRecommendation = $"1-stop optimal (Total: {bestTime / 60:F1} min)";
        }
        else if (bestTime == strategy.TwoStopTotalTime)
        {
            float delta = strategy.OneStopTotalTime - strategy.TwoStopTotalTime;
            strategy.MultiStopRecommendation = delta < 5f 
                ? $"2-stop marginal (saves {delta:F1}s vs 1-stop)"
                : $"2-stop optimal (saves {delta:F1}s vs 1-stop)";
        }
        else
        {
            float delta = strategy.TwoStopTotalTime - strategy.ThreeStopTotalTime;
            strategy.MultiStopRecommendation = delta < 5f
                ? $"3-stop marginal (saves {delta:F1}s vs 2-stop)"
                : $"3-stop optimal (saves {delta:F1}s vs 2-stop)";
        }
        
        // Build comparison string
        var comparisons = new List<string>();
        if (strategy.OneStopTotalTime < float.MaxValue)
            comparisons.Add($"1-stop: {strategy.OneStopTotalTime / 60:F1}min");
        if (strategy.TwoStopTotalTime < float.MaxValue)
            comparisons.Add($"2-stop: {strategy.TwoStopTotalTime / 60:F1}min");
        if (strategy.ThreeStopTotalTime < float.MaxValue)
            comparisons.Add($"3-stop: {strategy.ThreeStopTotalTime / 60:F1}min");
        
        strategy.MultiStopComparison = string.Join(" | ", comparisons);
    }
    
    /// <summary>
    /// Calculate partial refuel optimization (lighter car vs full tank)
    /// </summary>
    private void CalculatePartialRefuelOptimization(
        FuelData currentData,
        FuelAverages averages,
        PitStrategy strategy)
    {
        if (currentData.RaceLapsRemaining <= 0 || currentData.CanFinishWithoutStop)
            return;
        
        int lapsRemaining = currentData.RaceLapsRemaining;
        float avgFuel = currentData.AvgFuelPerLap;
        
        float fuelToFinish = lapsRemaining * avgFuel + currentData.FuelSputteringThreshold;
        float fuelDeficit = fuelToFinish - currentData.CurrentFuel;
        
        if (fuelDeficit <= 0)
            return;
        
        float fuelFlowRate = 2.5f; // L/s (default)
        float fuelWeightPenalty = 0.03f; // s/lap per liter
        
        // Full tank option
        float fullTankFuel = Math.Min(currentData.TankCapacity, currentData.TankCapacity - currentData.CurrentFuel);
        float fullTankRefuelTime = fullTankFuel / fuelFlowRate;
        float fullTankAvgWeight = (currentData.CurrentFuel + fullTankFuel) / 2f;
        float fullTankWeightPenalty = fullTankAvgWeight * fuelWeightPenalty * lapsRemaining;
        float fullTankTotalLoss = fullTankRefuelTime + fullTankWeightPenalty;
        
        // Partial refuel option
        float partialFuel = fuelDeficit;
        float partialRefuelTime = partialFuel / fuelFlowRate;
        float partialAvgWeight = (currentData.CurrentFuel + partialFuel) / 2f;
        float partialWeightPenalty = partialAvgWeight * fuelWeightPenalty * lapsRemaining;
        float partialTotalLoss = partialRefuelTime + partialWeightPenalty;
        
        float timeSaved = fullTankTotalLoss - partialTotalLoss;
        
        strategy.PartialRefuelAmount = partialFuel;
        strategy.PartialRefuelTimeSaved = timeSaved;
        
        if (timeSaved > 2f)
        {
            strategy.PartialRefuelRecommendation = 
                $"Partial fill ({partialFuel:F1}L) saves {timeSaved:F1}s vs full tank ({fullTankFuel:F1}L)";
        }
        else if (timeSaved < -2f)
        {
            strategy.PartialRefuelRecommendation = 
                $"Full tank faster (saves {Math.Abs(timeSaved):F1}s vs partial {partialFuel:F1}L)";
        }
        else
        {
            strategy.PartialRefuelRecommendation = 
                $"Marginal difference ({Math.Abs(timeSaved):F1}s) - either strategy viable";
        }
    }
}
