using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.Fuel;

/// <summary>
/// Service for calculating fuel saving strategies
/// Determines if fuel saving is needed and tracks progress
/// Phase 4: Extracted from FuelCalculatorService
/// </summary>
public class FuelSavingCalculator
{
    private readonly SessionPersistenceService? _persistenceService;
    
    public FuelSavingCalculator(SessionPersistenceService? persistenceService = null)
    {
        _persistenceService = persistenceService;
    }
    
    /// <summary>
    /// Calculate fuel saving requirements and progress
    /// </summary>
    public FuelSavingData Calculate(
        TelemetryData telemetry,
        FuelData currentData,
        FuelAverages averages,
        PitStrategy strategy)
    {
        var savingData = new FuelSavingData();
        
        // Check if race is ending
        bool isRaceEnding = currentData.RaceLapsRemaining <= 1 || 
                           (currentData.IsTimedSession && currentData.SessionTimeRemaining < 120);
        
        if (currentData.RaceLapsRemaining <= 0 || averages.L5 <= 0 || 
            !currentData.HasSufficientData || isRaceEnding)
        {
            savingData.NeedsFuelSaving = false;
            return savingData;
        }
        
        // Determine baseline average (prefer session average)
        float baselineAverage = currentData.AvgFuelPerLap_Session > 0 
            ? currentData.AvgFuelPerLap_Session 
            : averages.L5;
        
        if (baselineAverage <= 0)
            return savingData;
        
        // Check if we need fuel saving
        float fuelNeeded = (currentData.RaceLapsRemaining * baselineAverage) + currentData.FuelSputteringThreshold;
        float fuelDeficit = fuelNeeded - currentData.CurrentFuel;
        
        savingData.NeedsFuelSaving = fuelDeficit > 0 && !currentData.CanFinishWithoutStop;
        
        if (!savingData.NeedsFuelSaving)
            return savingData;
        
        // Calculate fuel saving target
        savingData.FuelSavingTarget = baselineAverage - (fuelDeficit / currentData.RaceLapsRemaining);
        
        // Validate target is achievable (can't save more than 30% typically)
        float maxSavingRate = baselineAverage * 0.70f; // Can save up to 30%
        if (savingData.FuelSavingTarget < maxSavingRate)
        {
            savingData.CanSaveFuelToFinish = false;
            savingData.StrategicAlert = "CRITICAL: Cannot save enough fuel - PIT REQUIRED";
            savingData.AlertSeverity = 2;
            return savingData;
        }
        
        savingData.CanSaveFuelToFinish = true;
        
        // Track current saving rate
        float currentL5 = averages.L5;
        if (currentL5 > 0 && currentL5 < baselineAverage)
        {
            savingData.CurrentSavingRate = currentL5;
            savingData.FuelSavingWorking = currentL5 <= savingData.FuelSavingTarget + 0.1f;
        }
        else if (currentData.FuelUsedLastLap > 0 && currentData.FuelUsedLastLap < baselineAverage)
        {
            savingData.CurrentSavingRate = currentData.FuelUsedLastLap;
            savingData.FuelSavingWorking = currentData.FuelUsedLastLap <= savingData.FuelSavingTarget + 0.1f;
        }
        else
        {
            savingData.CurrentSavingRate = baselineAverage;
            savingData.FuelSavingWorking = false;
        }
        
        // Calculate progress
        if (savingData.FuelSavingTarget > 0)
        {
            float savingRequired = baselineAverage - savingData.FuelSavingTarget;
            float savingAchieved = baselineAverage - savingData.CurrentSavingRate;
            savingData.SavingProgress = Math.Min(100f, (savingAchieved / savingRequired) * 100f);
        }
        
        // Compare pit stop vs fuel saving time
        CalculatePitVsSaveComparison(currentData, strategy, baselineAverage, savingData);
        
        // Generate strategic alerts
        GenerateStrategicAlerts(currentData, savingData, strategy);
        
        // Get historical context
        GetHistoricalContext(telemetry, currentData, savingData);
        
        return savingData;
    }
    
    private void CalculatePitVsSaveComparison(
        FuelData currentData,
        PitStrategy strategy,
        float baselineAverage,
        FuelSavingData savingData)
    {
        if (!savingData.NeedsFuelSaving)
            return;
        
        float avgLapTime = currentData.AverageLapTime > 0 ? currentData.AverageLapTime : 90f;
        float pitStopTime = currentData.EstimatedPitStopTime > 0 ? currentData.EstimatedPitStopTime : 30f;
        
        // Pit strategy time: normal laps + pit stop
        float pitStrategyTime = (currentData.RaceLapsRemaining * avgLapTime) + pitStopTime;
        
        // Fuel save strategy time: slower laps due to lifting/coasting
        float fuelSavingSlowdown = 0.5f; // Assume 0.5s/lap slower when fuel saving
        float saveStrategyTime = currentData.RaceLapsRemaining * (avgLapTime + fuelSavingSlowdown);
        
        savingData.StrategyTimeDelta = pitStrategyTime - saveStrategyTime;
        savingData.IsPittingFaster = savingData.StrategyTimeDelta < 0;
    }
    
    private void GenerateStrategicAlerts(
        FuelData currentData,
        FuelSavingData savingData,
        PitStrategy strategy)
    {
        // Critical: Can't finish without stop and can't save enough
        if (currentData.LapsRemaining < 1.2f && !currentData.CanFinishWithoutStop)
        {
            savingData.StrategicAlert = "CRITICAL: Less than 1.2 laps of fuel - PIT NOW!";
            savingData.AlertSeverity = 2;
            return;
        }
        
        // Critical: Fuel saving not working
        if (savingData.NeedsFuelSaving && !savingData.CanSaveFuelToFinish)
        {
            savingData.StrategicAlert = $"CRITICAL: Cannot save {savingData.FuelSavingTarget:F3}L/lap - PIT REQUIRED";
            savingData.AlertSeverity = 2;
            return;
        }
        
        // Warning: Pitting is faster but still trying to save
        if (savingData.NeedsFuelSaving && savingData.IsPittingFaster && 
            Math.Abs(savingData.StrategyTimeDelta) > 5.0f)
        {
            savingData.StrategicAlert = $"WARNING: Pitting saves {Math.Abs(savingData.StrategyTimeDelta):F1}s - consider pit stop";
            savingData.AlertSeverity = 1;
            return;
        }
        
        // Info: Fuel saving is working
        if (savingData.NeedsFuelSaving && savingData.FuelSavingWorking)
        {
            if (!savingData.IsPittingFaster)
            {
                savingData.StrategicAlert = $"Fuel saving working ({savingData.SavingProgress:F0}%) - faster than pitting";
                savingData.AlertSeverity = 0;
            }
            else
            {
                savingData.StrategicAlert = $"Fuel saving working ({savingData.SavingProgress:F0}%) - marginal vs pit stop";
                savingData.AlertSeverity = 0;
            }
        }
    }
    
    private void GetHistoricalContext(
        TelemetryData telemetry,
        FuelData currentData,
        FuelSavingData savingData)
    {
        var sessionStats = _persistenceService?.GetStatistics(telemetry.TrackName, telemetry.PlayerCarClass);
        
        if (sessionStats != null && sessionStats.HasSufficientData)
        {
            savingData.HasHistoricalData = true;
            
            // Compare current consumption to historical
            if (sessionStats.SessionAverageFuelPerLap > 0 && currentData.AvgFuelPerLap_Session > 0)
            {
                float variance = ((currentData.AvgFuelPerLap_Session - sessionStats.SessionAverageFuelPerLap) 
                                / sessionStats.SessionAverageFuelPerLap) * 100f;
                
                if (Math.Abs(variance) > 10f)
                {
                    savingData.HistoricalContext = variance > 0 
                        ? $"Using {variance:F0}% MORE fuel than historical average ({sessionStats.SessionAverageFuelPerLap:F3}L/lap)"
                        : $"Using {Math.Abs(variance):F0}% LESS fuel than historical average ({sessionStats.SessionAverageFuelPerLap:F3}L/lap)";
                }
                else
                {
                    savingData.HistoricalContext = $"Fuel usage matches historical data ({sessionStats.SessionAverageFuelPerLap:F3}L/lap)";
                }
            }
        }
    }
}
