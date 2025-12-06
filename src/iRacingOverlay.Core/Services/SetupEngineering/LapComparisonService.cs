using iRacingOverlay.Core.Services.Setup;

namespace iRacingOverlay.Core.Services.SetupEngineering;

/// <summary>
/// Lap Comparison Service for Setup Engineering
/// Compares baseline vs modified setup using statistical analysis
/// 
/// Features:
/// - Statistical significance testing (t-test, 95% confidence threshold)
/// - Sector-level delta analysis
/// - Outlier filtering (>3σ from mean)
/// - Confidence scoring for improvements/regressions
/// 
/// Usage:
/// var comparison = await service.CompareSetupsAsync(baselineSetupId, modifiedSetupId);
/// if (comparison.IsSignificant && comparison.ImprovementSeconds < 0)
///     Console.WriteLine($"✅ {-comparison.ImprovementSeconds:F3}s faster ({comparison.Confidence:P0})");
/// </summary>
public class LapComparisonService
{
    private readonly SetupDatabaseService _database;
    
    // Statistical constants
    private const float CONFIDENCE_THRESHOLD = 0.95f;  // 95% confidence required
    private const float OUTLIER_SIGMA = 3.0f;          // Outliers > 3σ from mean
    private const int MIN_LAPS_REQUIRED = 3;           // Minimum laps for comparison
    
    public LapComparisonService(SetupDatabaseService database)
    {
        _database = database;
    }
    
    /// <summary>
    /// Compare two setups (baseline vs modified)
    /// Returns statistical comparison with confidence scoring
    /// </summary>
    public async Task<SetupComparison> CompareSetupsAsync(
        string baselineSetupId,
        string modifiedSetupId)
    {
        // Load lap data for both setups
        var baselineLaps = await _database.GetLapsAsync(baselineSetupId, validOnly: true);
        var modifiedLaps = await _database.GetLapsAsync(modifiedSetupId, validOnly: true);
        
        if (baselineLaps.Count < MIN_LAPS_REQUIRED || modifiedLaps.Count < MIN_LAPS_REQUIRED)
        {
            return new SetupComparison
            {
                BaselineSetupId = baselineSetupId,
                ModifiedSetupId = modifiedSetupId,
                IsValid = false,
                ErrorMessage = $"Insufficient laps (need {MIN_LAPS_REQUIRED}+, got {baselineLaps.Count} baseline, {modifiedLaps.Count} modified)"
            };
        }
        
        // Extract lap times (filter nulls)
        var baselineTimes = baselineLaps
            .Where(l => l.LapTime.HasValue)
            .Select(l => l.LapTime!.Value)
            .ToList();
        
        var modifiedTimes = modifiedLaps
            .Where(l => l.LapTime.HasValue)
            .Select(l => l.LapTime!.Value)
            .ToList();
        
        // Filter outliers (>3σ from mean)
        baselineTimes = FilterOutliers(baselineTimes);
        modifiedTimes = FilterOutliers(modifiedTimes);
        
        if (baselineTimes.Count < MIN_LAPS_REQUIRED || modifiedTimes.Count < MIN_LAPS_REQUIRED)
        {
            return new SetupComparison
            {
                BaselineSetupId = baselineSetupId,
                ModifiedSetupId = modifiedSetupId,
                IsValid = false,
                ErrorMessage = "Too many outliers, not enough valid laps"
            };
        }
        
        // Calculate statistics
        var baselineStats = CalculateStatistics(baselineTimes);
        var modifiedStats = CalculateStatistics(modifiedTimes);
        
        // Perform t-test
        var tTestResult = PerformTTest(baselineTimes, modifiedTimes);
        
        // Sector-level analysis
        var sectorComparisons = CompareSectors(baselineLaps, modifiedLaps);
        
        // Build comparison result
        var comparison = new SetupComparison
        {
            BaselineSetupId = baselineSetupId,
            ModifiedSetupId = modifiedSetupId,
            IsValid = true,
            
            // Baseline stats
            BaselineLapCount = baselineTimes.Count,
            BaselineBestLap = baselineStats.Min,
            BaselineAvgLap = baselineStats.Mean,
            BaselineStdDev = baselineStats.StdDev,
            
            // Modified stats
            ModifiedLapCount = modifiedTimes.Count,
            ModifiedBestLap = modifiedStats.Min,
            ModifiedAvgLap = modifiedStats.Mean,
            ModifiedStdDev = modifiedStats.StdDev,
            
            // Comparison
            ImprovementSeconds = modifiedStats.Mean - baselineStats.Mean,  // Negative = faster
            ImprovementPercent = ((modifiedStats.Mean - baselineStats.Mean) / baselineStats.Mean) * 100f,
            Confidence = tTestResult.Confidence,
            IsSignificant = tTestResult.IsSignificant,
            
            // Sector analysis
            SectorComparisons = sectorComparisons
        };
        
        // Generate recommendation
        comparison.Recommendation = GenerateRecommendation(comparison);
        
        return comparison;
    }
    
    /// <summary>
    /// Filter outliers using 3-sigma rule
    /// </summary>
    private List<float> FilterOutliers(List<float> values)
    {
        if (values.Count < 3)
            return values;  // Not enough data to filter
        
        var stats = CalculateStatistics(values);
        var threshold = OUTLIER_SIGMA * stats.StdDev;
        
        return values
            .Where(v => Math.Abs(v - stats.Mean) <= threshold)
            .ToList();
    }
    
    /// <summary>
    /// Calculate basic statistics (mean, stddev, min, max)
    /// </summary>
    private Statistics CalculateStatistics(List<float> values)
    {
        if (values.Count == 0)
            return new Statistics();
        
        float mean = values.Average();
        float variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        float stdDev = (float)Math.Sqrt(variance);
        
        return new Statistics
        {
            Count = values.Count,
            Mean = mean,
            StdDev = stdDev,
            Min = values.Min(),
            Max = values.Max()
        };
    }
    
    /// <summary>
    /// Perform Welch's t-test (unequal variance t-test)
    /// Returns confidence that the means are different
    /// </summary>
    private TTestResult PerformTTest(List<float> sample1, List<float> sample2)
    {
        var stats1 = CalculateStatistics(sample1);
        var stats2 = CalculateStatistics(sample2);
        
        // Welch's t-statistic
        float se1 = stats1.StdDev * stats1.StdDev / stats1.Count;
        float se2 = stats2.StdDev * stats2.StdDev / stats2.Count;
        float tStatistic = (stats1.Mean - stats2.Mean) / (float)Math.Sqrt(se1 + se2);
        
        // Degrees of freedom (Welch-Satterthwaite equation)
        float df = (float)Math.Pow(se1 + se2, 2) / 
                   (float)(Math.Pow(se1, 2) / (stats1.Count - 1) + Math.Pow(se2, 2) / (stats2.Count - 1));
        
        // Convert t-statistic to confidence (approximation)
        // For df > 30, use normal distribution approximation
        float confidence = 0f;
        if (df > 30)
        {
            // Use standard normal CDF approximation
            float z = Math.Abs(tStatistic);
            confidence = 1f - 2f * (1f / (1f + (float)Math.Exp(1.65451f * z)));  // Approximate
        }
        else
        {
            // For small samples, use conservative estimate
            confidence = Math.Abs(tStatistic) > 2.0f ? 0.95f : 0.80f;
        }
        
        return new TTestResult
        {
            TStatistic = tStatistic,
            DegreesOfFreedom = df,
            Confidence = Math.Min(confidence, 0.99f),  // Cap at 99%
            IsSignificant = confidence >= CONFIDENCE_THRESHOLD
        };
    }
    
    /// <summary>
    /// Compare sector times (sector-level delta analysis)
    /// </summary>
    private List<SectorComparison> CompareSectors(
        List<LapTelemetrySummary> baselineLaps,
        List<LapTelemetrySummary> modifiedLaps)
    {
        var sectorComparisons = new List<SectorComparison>();
        
        // Sector 1
        var baselineS1 = baselineLaps.Where(l => l.Sector1.HasValue).Select(l => l.Sector1!.Value).ToList();
        var modifiedS1 = modifiedLaps.Where(l => l.Sector1.HasValue).Select(l => l.Sector1!.Value).ToList();
        if (baselineS1.Count >= 3 && modifiedS1.Count >= 3)
        {
            var s1Stats = CompareSector(baselineS1, modifiedS1);
            sectorComparisons.Add(new SectorComparison
            {
                SectorNumber = 1,
                BaselineAvg = s1Stats.BaselineAvg,
                ModifiedAvg = s1Stats.ModifiedAvg,
                DeltaSeconds = s1Stats.DeltaSeconds,
                Confidence = s1Stats.Confidence,
                IsSignificant = s1Stats.IsSignificant
            });
        }
        
        // Sector 2
        var baselineS2 = baselineLaps.Where(l => l.Sector2.HasValue).Select(l => l.Sector2!.Value).ToList();
        var modifiedS2 = modifiedLaps.Where(l => l.Sector2.HasValue).Select(l => l.Sector2!.Value).ToList();
        if (baselineS2.Count >= 3 && modifiedS2.Count >= 3)
        {
            var s2Stats = CompareSector(baselineS2, modifiedS2);
            sectorComparisons.Add(new SectorComparison
            {
                SectorNumber = 2,
                BaselineAvg = s2Stats.BaselineAvg,
                ModifiedAvg = s2Stats.ModifiedAvg,
                DeltaSeconds = s2Stats.DeltaSeconds,
                Confidence = s2Stats.Confidence,
                IsSignificant = s2Stats.IsSignificant
            });
        }
        
        // Sector 3
        var baselineS3 = baselineLaps.Where(l => l.Sector3.HasValue).Select(l => l.Sector3!.Value).ToList();
        var modifiedS3 = modifiedLaps.Where(l => l.Sector3.HasValue).Select(l => l.Sector3!.Value).ToList();
        if (baselineS3.Count >= 3 && modifiedS3.Count >= 3)
        {
            var s3Stats = CompareSector(baselineS3, modifiedS3);
            sectorComparisons.Add(new SectorComparison
            {
                SectorNumber = 3,
                BaselineAvg = s3Stats.BaselineAvg,
                ModifiedAvg = s3Stats.ModifiedAvg,
                DeltaSeconds = s3Stats.DeltaSeconds,
                Confidence = s3Stats.Confidence,
                IsSignificant = s3Stats.IsSignificant
            });
        }
        
        return sectorComparisons;
    }
    
    /// <summary>
    /// Compare a single sector
    /// </summary>
    private (float BaselineAvg, float ModifiedAvg, float DeltaSeconds, float Confidence, bool IsSignificant) CompareSector(
        List<float> baselineTimes,
        List<float> modifiedTimes)
    {
        var baselineStats = CalculateStatistics(baselineTimes);
        var modifiedStats = CalculateStatistics(modifiedTimes);
        var tTest = PerformTTest(baselineTimes, modifiedTimes);
        
        return (
            baselineStats.Mean,
            modifiedStats.Mean,
            modifiedStats.Mean - baselineStats.Mean,
            tTest.Confidence,
            tTest.IsSignificant
        );
    }
    
    /// <summary>
    /// Generate human-readable recommendation
    /// </summary>
    private string GenerateRecommendation(SetupComparison comparison)
    {
        if (!comparison.IsValid)
            return "⚠️ Cannot generate recommendation (insufficient data)";
        
        if (!comparison.IsSignificant)
            return "⚠️ No statistically significant difference (< 95% confidence). Need more laps or larger setup change.";
        
        if (comparison.ImprovementSeconds < -0.05f)  // Faster by > 50ms
        {
            var gainSector = comparison.SectorComparisons
                .Where(s => s.IsSignificant && s.DeltaSeconds < 0)
                .OrderBy(s => s.DeltaSeconds)
                .FirstOrDefault();
            
            if (gainSector != null)
            {
                return $"✅ IMPROVEMENT: {-comparison.ImprovementSeconds:F3}s faster ({comparison.Confidence:P0} confidence). " +
                       $"Biggest gain in Sector {gainSector.SectorNumber} ({-gainSector.DeltaSeconds:F3}s).";
            }
            
            return $"✅ IMPROVEMENT: {-comparison.ImprovementSeconds:F3}s faster ({comparison.Confidence:P0} confidence).";
        }
        else if (comparison.ImprovementSeconds > 0.05f)  // Slower by > 50ms
        {
            var lossSector = comparison.SectorComparisons
                .Where(s => s.IsSignificant && s.DeltaSeconds > 0)
                .OrderByDescending(s => s.DeltaSeconds)
                .FirstOrDefault();
            
            if (lossSector != null)
            {
                return $"❌ REGRESSION: {comparison.ImprovementSeconds:F3}s slower ({comparison.Confidence:P0} confidence). " +
                       $"Biggest loss in Sector {lossSector.SectorNumber} (+{lossSector.DeltaSeconds:F3}s).";
            }
            
            return $"❌ REGRESSION: {comparison.ImprovementSeconds:F3}s slower ({comparison.Confidence:P0} confidence).";
        }
        else
        {
            return $"➡️ NEUTRAL: {Math.Abs(comparison.ImprovementSeconds):F3}s difference (within margin of error).";
        }
    }
}

// ===== DATA MODELS =====

public class SetupComparison
{
    public string BaselineSetupId { get; set; } = "";
    public string ModifiedSetupId { get; set; } = "";
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Baseline statistics
    public int BaselineLapCount { get; set; }
    public float BaselineBestLap { get; set; }
    public float BaselineAvgLap { get; set; }
    public float BaselineStdDev { get; set; }
    
    // Modified statistics
    public int ModifiedLapCount { get; set; }
    public float ModifiedBestLap { get; set; }
    public float ModifiedAvgLap { get; set; }
    public float ModifiedStdDev { get; set; }
    
    // Comparison results
    public float ImprovementSeconds { get; set; }  // Negative = faster
    public float ImprovementPercent { get; set; }
    public float Confidence { get; set; }          // 0.0 - 1.0 (0.95 = 95%)
    public bool IsSignificant { get; set; }        // True if >= 95% confidence
    
    // Sector analysis
    public List<SectorComparison> SectorComparisons { get; set; } = new();
    
    // Recommendation
    public string Recommendation { get; set; } = "";
}

public class SectorComparison
{
    public int SectorNumber { get; set; }
    public float BaselineAvg { get; set; }
    public float ModifiedAvg { get; set; }
    public float DeltaSeconds { get; set; }  // Negative = faster
    public float Confidence { get; set; }
    public bool IsSignificant { get; set; }
}

internal class Statistics
{
    public int Count { get; set; }
    public float Mean { get; set; }
    public float StdDev { get; set; }
    public float Min { get; set; }
    public float Max { get; set; }
}

internal class TTestResult
{
    public float TStatistic { get; set; }
    public float DegreesOfFreedom { get; set; }
    public float Confidence { get; set; }
    public bool IsSignificant { get; set; }
}
