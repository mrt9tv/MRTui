using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Turn-by-turn telemetry learning system
/// Analyzes sector-level performance per lap, track, and vehicle combination
/// 
/// Purpose:
/// - Learn optimal speed/throttle/braking profiles for each corner
/// - Detect performance anomalies (slow corners, lockups, oversteer)
/// - Compare driver performance across sectors over time
/// - Build corner-specific insights for Setup Engineering and Strategy Scouting
/// 
/// Storage:
/// - Per-vehicle, per-track sector profiles in %APPDATA%/MRTOverlay/SectorProfiles/
/// - JSON format: {TrackName}/{CarName}/sector_{N}.json
/// </summary>
public class SectorTelemetryAnalyzer
{
    private readonly string _sectorProfilesDirectory;
    private readonly object _analysisLock = new object();
    
    // In-memory cache of sector profiles (loaded on demand)
    private readonly Dictionary<string, VehicleSectorProfile> _profileCache = new();
    
    // Current lap's sector telemetry buffer
    private readonly List<SectorTelemetrySlice> _currentSectorBuffer = new();
    
    // Statistics
    private int _sectorsAnalyzed = 0;
    private int _profilesUpdated = 0;
    
    public SectorTelemetryAnalyzer()
    {
        // Initialize directory in %APPDATA%/MRTOverlay/SectorProfiles/
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string mrtOverlayPath = Path.Combine(appDataPath, "MRTOverlay");
        _sectorProfilesDirectory = Path.Combine(mrtOverlayPath, "SectorProfiles");
        
        Directory.CreateDirectory(_sectorProfilesDirectory);
        
        LogInfo("SectorTelemetryAnalyzer initialized");
    }
    
    /// <summary>
    /// Analyze completed lap and extract sector-level insights
    /// Called by TelemetryCollectionEngine after each lap
    /// </summary>
    public SectorAnalysisResult AnalyzeLap(
        string trackName,
        string carName,
        int lapNumber,
        List<TelemetrySnapshot> lapTelemetry,
        TrackSectors trackSectors)
    {
        lock (_analysisLock)
        {
            if (lapTelemetry.Count == 0 || trackSectors.Sectors.Count == 0)
            {
                return new SectorAnalysisResult { Success = false, Message = "Insufficient data" };
            }
            
            // Load or create vehicle sector profile
            string profileKey = GetProfileKey(trackName, carName);
            var profile = GetOrCreateProfile(profileKey, trackName, carName, trackSectors.Sectors.Count);
            
            // Split lap telemetry into sectors
            var sectorSlices = SplitLapIntoSectors(lapTelemetry, trackSectors);
            
            if (sectorSlices.Count != trackSectors.Sectors.Count)
            {
                LogWarning($"Sector count mismatch: expected {trackSectors.Sectors.Count}, got {sectorSlices.Count}");
                return new SectorAnalysisResult { Success = false, Message = "Sector count mismatch" };
            }
            
            // Analyze each sector
            var sectorResults = new List<SectorPerformance>();
            for (int i = 0; i < sectorSlices.Count; i++)
            {
                var slice = sectorSlices[i];
                var sectorProfile = profile.Sectors[i];
                
                // Calculate sector metrics
                var performance = CalculateSectorPerformance(slice);
                sectorResults.Add(performance);
                
                // Update historical profile
                UpdateSectorProfile(sectorProfile, performance, lapNumber);
                
                _sectorsAnalyzed++;
            }
            
            // Save updated profile
            SaveProfile(profileKey, profile);
            _profilesUpdated++;
            
            // Detect anomalies
            var anomalies = DetectAnomalies(sectorResults, profile);
            
            LogInfo($"Lap {lapNumber} analyzed: {sectorResults.Count} sectors, {anomalies.Count} anomalies");
            
            return new SectorAnalysisResult
            {
                Success = true,
                LapNumber = lapNumber,
                SectorPerformances = sectorResults,
                Anomalies = anomalies,
                ProfileKey = profileKey
            };
        }
    }
    
    /// <summary>
    /// Split lap telemetry into sector slices based on LapDistPct
    /// </summary>
    private List<SectorTelemetrySlice> SplitLapIntoSectors(
        List<TelemetrySnapshot> lapTelemetry,
        TrackSectors trackSectors)
    {
        var slices = new List<SectorTelemetrySlice>();
        
        for (int i = 0; i < trackSectors.Sectors.Count; i++)
        {
            var sector = trackSectors.Sectors[i];
            
            // Determine sector boundaries (current sector start to next sector start)
            float sectorStartPct = sector.SectorStartPct;
            float sectorEndPct = (i + 1 < trackSectors.Sectors.Count)
                ? trackSectors.Sectors[i + 1].SectorStartPct
                : 1.0f;  // Last sector wraps to finish line
            
            // Extract telemetry samples within this sector
            var sectorSamples = lapTelemetry
                .Where(t => t.LapDistPct >= sectorStartPct && t.LapDistPct < sectorEndPct)
                .ToList();
            
            if (sectorSamples.Count == 0)
            {
                // Handle wrap-around case (sector crosses finish line)
                if (sectorEndPct <= sectorStartPct)
                {
                    sectorSamples = lapTelemetry
                        .Where(t => t.LapDistPct >= sectorStartPct || t.LapDistPct < sectorEndPct)
                        .ToList();
                }
            }
            
            slices.Add(new SectorTelemetrySlice
            {
                SectorNumber = sector.SectorNum,
                StartPct = sectorStartPct,
                EndPct = sectorEndPct,
                Samples = sectorSamples
            });
        }
        
        return slices;
    }
    
    /// <summary>
    /// Calculate performance metrics for a sector
    /// </summary>
    private SectorPerformance CalculateSectorPerformance(SectorTelemetrySlice slice)
    {
        if (slice.Samples.Count == 0)
        {
            return new SectorPerformance { SectorNumber = slice.SectorNumber, IsValid = false };
        }
        
        var samples = slice.Samples;
        
        // Speed analysis
        float avgSpeed = samples.Average(s => s.Speed);
        float maxSpeed = samples.Max(s => s.Speed);
        float minSpeed = samples.Min(s => s.Speed);
        
        // Throttle analysis (average, time at full throttle)
        float avgThrottle = samples.Average(s => s.Throttle);
        float fullThrottlePct = samples.Count(s => s.Throttle > 0.95f) / (float)samples.Count;
        
        // G-force analysis (peak cornering)
        float maxLatAccel = samples.Max(s => Math.Abs(s.LatAccel ?? 0f));
        float maxLongAccel = samples.Max(s => Math.Abs(s.LongAccel ?? 0f));
        
        // Time in sector (approximate from sample count and timestamp delta)
        float sectorTime = 0f;
        if (samples.Count > 1)
        {
            var timeSpan = samples.Last().Timestamp - samples.First().Timestamp;
            sectorTime = (float)timeSpan.TotalSeconds;
        }
        
        // Tire temp analysis (average across all 4 tires)
        float avgTireTemp = 0f;
        int tempSampleCount = 0;
        foreach (var sample in samples)
        {
            if (sample.LFtempCM.HasValue) { avgTireTemp += sample.LFtempCM.Value; tempSampleCount++; }
            if (sample.RFtempCM.HasValue) { avgTireTemp += sample.RFtempCM.Value; tempSampleCount++; }
            if (sample.LRtempCM.HasValue) { avgTireTemp += sample.LRtempCM.Value; tempSampleCount++; }
            if (sample.RRtempCM.HasValue) { avgTireTemp += sample.RRtempCM.Value; tempSampleCount++; }
        }
        avgTireTemp = tempSampleCount > 0 ? avgTireTemp / tempSampleCount : 0f;
        
        // Detect braking (negative LongAccel)
        bool hasBraking = samples.Any(s => (s.LongAccel ?? 0f) < -1.0f);  // > 1G braking
        
        return new SectorPerformance
        {
            SectorNumber = slice.SectorNumber,
            SectorTime = sectorTime,
            AvgSpeed = avgSpeed,
            MaxSpeed = maxSpeed,
            MinSpeed = minSpeed,
            AvgThrottle = avgThrottle,
            FullThrottlePct = fullThrottlePct,
            MaxLatAccel = maxLatAccel,
            MaxLongAccel = maxLongAccel,
            AvgTireTemp = avgTireTemp,
            HasBraking = hasBraking,
            IsValid = true
        };
    }
    
    /// <summary>
    /// Update historical sector profile with new lap data
    /// </summary>
    private void UpdateSectorProfile(
        SectorProfile sectorProfile,
        SectorPerformance performance,
        int lapNumber)
    {
        // Update best sector time
        if (performance.SectorTime > 0f)
        {
            if (sectorProfile.BestSectorTime == 0f || performance.SectorTime < sectorProfile.BestSectorTime)
            {
                sectorProfile.BestSectorTime = performance.SectorTime;
                sectorProfile.BestLapNumber = lapNumber;
            }
        }
        
        // Update average metrics (exponential moving average, weight = 0.2)
        float alpha = 0.2f;
        sectorProfile.AvgSpeed = sectorProfile.AvgSpeed * (1 - alpha) + performance.AvgSpeed * alpha;
        sectorProfile.AvgThrottle = sectorProfile.AvgThrottle * (1 - alpha) + performance.AvgThrottle * alpha;
        sectorProfile.AvgLatAccel = sectorProfile.AvgLatAccel * (1 - alpha) + performance.MaxLatAccel * alpha;
        
        // Update lap count
        sectorProfile.LapCount++;
        sectorProfile.LastUpdated = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Detect performance anomalies compared to historical profile
    /// </summary>
    private List<SectorAnomaly> DetectAnomalies(
        List<SectorPerformance> sectorPerformances,
        VehicleSectorProfile profile)
    {
        var anomalies = new List<SectorAnomaly>();
        
        for (int i = 0; i < sectorPerformances.Count; i++)
        {
            var perf = sectorPerformances[i];
            var profileSector = profile.Sectors[i];
            
            // Need at least 5 laps of history to detect anomalies
            if (profileSector.LapCount < 5 || !perf.IsValid)
                continue;
            
            // Anomaly 1: Sector time > 10% slower than best
            if (perf.SectorTime > 0f && profileSector.BestSectorTime > 0f)
            {
                float delta = perf.SectorTime - profileSector.BestSectorTime;
                float deltaPct = (delta / profileSector.BestSectorTime) * 100f;
                
                if (deltaPct > 10f)
                {
                    anomalies.Add(new SectorAnomaly
                    {
                        SectorNumber = perf.SectorNumber,
                        Type = AnomalyType.SlowSector,
                        Description = $"Sector {perf.SectorNumber + 1}: {deltaPct:F1}% slower than best ({delta:F2}s)",
                        Severity = deltaPct > 20f ? AnomalySeverity.High : AnomalySeverity.Medium
                    });
                }
            }
            
            // Anomaly 2: Speed significantly lower than average
            if (perf.AvgSpeed > 0f && profileSector.AvgSpeed > 0f)
            {
                float speedDelta = profileSector.AvgSpeed - perf.AvgSpeed;
                float speedDeltaPct = (speedDelta / profileSector.AvgSpeed) * 100f;
                
                if (speedDeltaPct > 15f)
                {
                    anomalies.Add(new SectorAnomaly
                    {
                        SectorNumber = perf.SectorNumber,
                        Type = AnomalyType.LowSpeed,
                        Description = $"Sector {perf.SectorNumber + 1}: Avg speed {speedDeltaPct:F1}% below normal",
                        Severity = AnomalySeverity.Medium
                    });
                }
            }
            
            // Anomaly 3: Throttle usage significantly lower
            if (perf.AvgThrottle > 0f && profileSector.AvgThrottle > 0f)
            {
                float throttleDelta = profileSector.AvgThrottle - perf.AvgThrottle;
                
                if (throttleDelta > 0.2f)  // 20% less throttle
                {
                    anomalies.Add(new SectorAnomaly
                    {
                        SectorNumber = perf.SectorNumber,
                        Type = AnomalyType.LowThrottle,
                        Description = $"Sector {perf.SectorNumber + 1}: Throttle usage {throttleDelta * 100:F0}% below normal",
                        Severity = AnomalySeverity.Low
                    });
                }
            }
        }
        
        return anomalies;
    }
    
    /// <summary>
    /// Get profile key for cache lookup
    /// </summary>
    private string GetProfileKey(string trackName, string carName)
    {
        return $"{trackName}_{carName}".Replace(" ", "_").Replace("/", "_");
    }
    
    /// <summary>
    /// Get or create vehicle sector profile
    /// </summary>
    private VehicleSectorProfile GetOrCreateProfile(
        string profileKey,
        string trackName,
        string carName,
        int sectorCount)
    {
        // Check cache first
        if (_profileCache.TryGetValue(profileKey, out var cachedProfile))
        {
            return cachedProfile;
        }
        
        // Try to load from disk
        string profilePath = Path.Combine(_sectorProfilesDirectory, $"{profileKey}.json");
        if (File.Exists(profilePath))
        {
            try
            {
                string json = File.ReadAllText(profilePath);
                var profile = System.Text.Json.JsonSerializer.Deserialize<VehicleSectorProfile>(json);
                if (profile != null)
                {
                    _profileCache[profileKey] = profile;
                    LogInfo($"Loaded profile: {profileKey} ({profile.LapCount} laps)");
                    return profile;
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to load profile {profileKey}: {ex.Message}");
            }
        }
        
        // Create new profile
        var newProfile = new VehicleSectorProfile
        {
            TrackName = trackName,
            CarName = carName,
            LapCount = 0,
            Sectors = Enumerable.Range(0, sectorCount)
                .Select(i => new SectorProfile { SectorNumber = i })
                .ToList()
        };
        
        _profileCache[profileKey] = newProfile;
        LogInfo($"Created new profile: {profileKey}");
        return newProfile;
    }
    
    /// <summary>
    /// Save profile to disk
    /// </summary>
    private void SaveProfile(string profileKey, VehicleSectorProfile profile)
    {
        try
        {
            string profilePath = Path.Combine(_sectorProfilesDirectory, $"{profileKey}.json");
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            
            string json = System.Text.Json.JsonSerializer.Serialize(profile, options);
            File.WriteAllText(profilePath, json);
            
            LogDebug($"Profile saved: {profileKey}");
        }
        catch (Exception ex)
        {
            LogError($"Failed to save profile {profileKey}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Get insights for a specific sector (for UI display)
    /// </summary>
    public SectorInsights GetSectorInsights(string trackName, string carName, int sectorNumber)
    {
        lock (_analysisLock)
        {
            string profileKey = GetProfileKey(trackName, carName);
            if (!_profileCache.TryGetValue(profileKey, out var profile))
            {
                // Try to load from disk
                profile = GetOrCreateProfile(profileKey, trackName, carName, sectorNumber + 1);
            }
            
            if (sectorNumber >= profile.Sectors.Count)
            {
                return new SectorInsights { SectorNumber = sectorNumber, HasData = false };
            }
            
            var sector = profile.Sectors[sectorNumber];
            
            return new SectorInsights
            {
                SectorNumber = sectorNumber,
                HasData = sector.LapCount > 0,
                BestSectorTime = sector.BestSectorTime,
                BestLapNumber = sector.BestLapNumber,
                AvgSpeed = sector.AvgSpeed,
                AvgThrottle = sector.AvgThrottle,
                AvgLatAccel = sector.AvgLatAccel,
                LapCount = sector.LapCount,
                LastUpdated = sector.LastUpdated
            };
        }
    }
    
    // Logging methods
    private void LogInfo(string message) => Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} SectorTelemetryAnalyzer - {message}");
    private void LogWarning(string message) => Console.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss} SectorTelemetryAnalyzer - {message}");
    private void LogError(string message) => Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} SectorTelemetryAnalyzer - {message}");
    private void LogDebug(string message) => Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss} SectorTelemetryAnalyzer - {message}");
}

// ===== DATA MODELS =====

/// <summary>
/// Vehicle-specific sector profile (learned over time)
/// </summary>
public class VehicleSectorProfile
{
    public string TrackName { get; set; } = "";
    public string CarName { get; set; } = "";
    public int LapCount { get; set; }
    public List<SectorProfile> Sectors { get; set; } = new();
}

/// <summary>
/// Historical profile for a single sector
/// </summary>
public class SectorProfile
{
    public int SectorNumber { get; set; }
    public float BestSectorTime { get; set; }
    public int BestLapNumber { get; set; }
    public float AvgSpeed { get; set; }
    public float AvgThrottle { get; set; }
    public float AvgLatAccel { get; set; }
    public int LapCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Telemetry samples for a single sector
/// </summary>
public class SectorTelemetrySlice
{
    public int SectorNumber { get; set; }
    public float StartPct { get; set; }
    public float EndPct { get; set; }
    public List<TelemetrySnapshot> Samples { get; set; } = new();
}

/// <summary>
/// Performance metrics for a sector
/// </summary>
public class SectorPerformance
{
    public int SectorNumber { get; set; }
    public float SectorTime { get; set; }
    public float AvgSpeed { get; set; }
    public float MaxSpeed { get; set; }
    public float MinSpeed { get; set; }
    public float AvgThrottle { get; set; }
    public float FullThrottlePct { get; set; }
    public float MaxLatAccel { get; set; }
    public float MaxLongAccel { get; set; }
    public float AvgTireTemp { get; set; }
    public bool HasBraking { get; set; }
    public bool IsValid { get; set; }
}

/// <summary>
/// Result of sector analysis
/// </summary>
public class SectorAnalysisResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int LapNumber { get; set; }
    public List<SectorPerformance> SectorPerformances { get; set; } = new();
    public List<SectorAnomaly> Anomalies { get; set; } = new();
    public string ProfileKey { get; set; } = "";
}

/// <summary>
/// Detected performance anomaly
/// </summary>
public class SectorAnomaly
{
    public int SectorNumber { get; set; }
    public AnomalyType Type { get; set; }
    public string Description { get; set; } = "";
    public AnomalySeverity Severity { get; set; }
}

/// <summary>
/// Types of sector anomalies
/// </summary>
public enum AnomalyType
{
    SlowSector,      // Sector time significantly slower than best
    LowSpeed,        // Average speed lower than normal
    LowThrottle,     // Throttle usage lower than normal
    HighLatAccel,    // Excessive cornering G-forces (potential setup issue)
    Lockup           // Brake lockup detected
}

/// <summary>
/// Anomaly severity levels
/// </summary>
public enum AnomalySeverity
{
    Low,     // Minor deviation
    Medium,  // Noticeable deviation
    High     // Significant deviation
}

/// <summary>
/// Sector insights for UI display
/// </summary>
public class SectorInsights
{
    public int SectorNumber { get; set; }
    public bool HasData { get; set; }
    public float BestSectorTime { get; set; }
    public int BestLapNumber { get; set; }
    public float AvgSpeed { get; set; }
    public float AvgThrottle { get; set; }
    public float AvgLatAccel { get; set; }
    public int LapCount { get; set; }
    public DateTime LastUpdated { get; set; }
}
