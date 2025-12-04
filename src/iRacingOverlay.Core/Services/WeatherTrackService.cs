using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Service for tracking weather and track condition trends
/// Monitors temperature changes, weather patterns, and grip evolution
/// </summary>
public class WeatherTrackService
{
    private readonly List<WeatherSnapshot> _weatherHistory = new();
    private const int MAX_HISTORY = 600; // Keep last 600 samples (100 seconds at 6Hz) for trend calculation
    
    // FIX: Baseline temperature tracking for session start
    // Captures initial temperatures to show +/- changes throughout session
    private float? _baselineAirTemp = null;
    private float? _baselineTrackTemp = null;
    private int? _baselineSessionId = null; // Track session changes to reset baseline
    
    /// <summary>
    /// Weather snapshot data
    /// </summary>
    public class WeatherSnapshot
    {
        public DateTime Timestamp { get; set; }
        public float AirTemp { get; set; }
        public float TrackTemp { get; set; }
        public int Skies { get; set; }
        public int WeatherType { get; set; }
        public float FogLevel { get; set; }
        public int TrackWetness { get; set; }
        public float AirDensity { get; set; }
        public float WindVel { get; set; }
        public float WindDir { get; set; }
        public float RelativeHumidity { get; set; }
        public float AirPressure { get; set; }
        public bool WeatherDeclaredWet { get; set; }
    }

    /// <summary>
    /// Current weather conditions summary
    /// </summary>
    public class WeatherConditions
    {
        public float CurrentAirTemp { get; set; }
        public float CurrentTrackTemp { get; set; }
        public float AirTempTrend { get; set; } // Degrees per 10 minutes
        public float TrackTempTrend { get; set; } // Degrees per 10 minutes
        public string SkyCondition { get; set; } = "Clear";
        public string WeatherStatus { get; set; } = "Dry";
        public string TrackWetnessDescription { get; set; } = "Dry";
        public int TrackWetnessLevel { get; set; } // 0-6 enum value
        public float FogLevel { get; set; }
        public bool IsWeatherChanging { get; set; }
        public string ChangeDescription { get; set; } = string.Empty;
        
        // FIX: Baseline temperature deltas (change from session start)
        public float? BaselineAirTemp { get; set; } // Starting air temp
        public float? BaselineTrackTemp { get; set; } // Starting track temp
        public float AirTempDelta { get; set; } // Change from baseline (°C)
        public float TrackTempDelta { get; set; } // Change from baseline (°C)
        
        // FIX: Comprehensive weather data from SDK
        public float WindSpeed { get; set; } // m/s
        public float WindDirection { get; set; } // radians
        public string WindDirectionCardinal { get; set; } = ""; // N, NE, E, etc.
        public float RelativeHumidity { get; set; } // %
        public float AirPressure { get; set; } // Pa
        public float AirDensity { get; set; } // kg/m³
        public bool IsWetTrack { get; set; } // Rain tires allowed
        public int HistorySampleCount { get; set; } // Number of samples collected (for trend calculation status)
        public int Skies { get; set; } // 0=Clear, 1=PartlyCloudy, 2=MostlyCloudy, 3=Overcast
    }

    /// <summary>
    /// Update weather tracking with new telemetry
    /// FIX: Captures baseline temperatures on first update or session change
    /// </summary>
    public void Update(TelemetryData data)
    {
        // FIX: Detect session change and reset baseline
        // SessionNum changes when entering new practice/qual/race session
        if (_baselineSessionId == null || data.SessionNum != _baselineSessionId)
        {
            // New session detected - set baseline temperatures
            _baselineAirTemp = data.AirTemp;
            _baselineTrackTemp = data.TrackTemp;
            _baselineSessionId = data.SessionNum;
            System.Diagnostics.Debug.WriteLine($"[WeatherTrack] Session {data.SessionNum} baseline set: Air={_baselineAirTemp:F1}°C, Track={_baselineTrackTemp:F1}°C");
        }
        
        var snapshot = new WeatherSnapshot
        {
            Timestamp = DateTime.Now,
            AirTemp = data.AirTemp,
            TrackTemp = data.TrackTemp,
            Skies = data.Skies,
            WeatherType = data.WeatherType,
            FogLevel = data.FogLevel,
            TrackWetness = data.TrackWetness,
            AirDensity = data.AirDensity,
            WindVel = data.WindVel,
            WindDir = data.WindDir,
            RelativeHumidity = data.RelativeHumidity,
            AirPressure = data.AirPressure,
            WeatherDeclaredWet = data.WeatherDeclaredWet
        };

        _weatherHistory.Add(snapshot);

        // Limit history size
        if (_weatherHistory.Count > MAX_HISTORY)
            _weatherHistory.RemoveAt(0);
    }

    /// <summary>
    /// Get current weather conditions with trends
    /// FIX: Now includes baseline temperature deltas (change from session start)
    /// </summary>
    public WeatherConditions GetCurrentConditions()
    {
        if (_weatherHistory.Count == 0)
            return new WeatherConditions();

        var latest = _weatherHistory[^1];
        var conditions = new WeatherConditions
        {
            CurrentAirTemp = latest.AirTemp,
            CurrentTrackTemp = latest.TrackTemp,
            SkyCondition = GetSkyDescription(latest.Skies),
            WeatherStatus = GetWeatherDescription(latest.WeatherType),
            TrackWetnessDescription = GetTrackWetnessDescription(latest.TrackWetness),
            TrackWetnessLevel = latest.TrackWetness,
            FogLevel = latest.FogLevel,
            
            // FIX: Include baseline temperatures and deltas
            BaselineAirTemp = _baselineAirTemp,
            BaselineTrackTemp = _baselineTrackTemp,
            AirTempDelta = _baselineAirTemp.HasValue ? latest.AirTemp - _baselineAirTemp.Value : 0f,
            TrackTempDelta = _baselineTrackTemp.HasValue ? latest.TrackTemp - _baselineTrackTemp.Value : 0f,
            
            // FIX: Comprehensive weather data
            WindSpeed = latest.WindVel,
            WindDirection = latest.WindDir,
            WindDirectionCardinal = GetWindDirectionCardinal(latest.WindDir),
            RelativeHumidity = latest.RelativeHumidity,
            AirPressure = latest.AirPressure,
            AirDensity = latest.AirDensity,
            IsWetTrack = latest.WeatherDeclaredWet,
            HistorySampleCount = _weatherHistory.Count,
            Skies = latest.Skies
        };

        // Calculate trends if we have enough history
        // FIX: Require at least 100 seconds (600 samples @ 6Hz) to avoid extrapolation errors
        // Problem: 20 samples = 3.3 seconds → 0.1°C change extrapolates to 18°C/10min (absurd!)
        // Solution: Use minimum 100 seconds of data, ideally calculate over most recent 10 minutes
        if (_weatherHistory.Count >= 600) // 100 seconds minimum
        {
            // Find the snapshot closest to 10 minutes ago (or use oldest if less than 10min history)
            var tenMinutesAgo = latest.Timestamp.AddMinutes(-10);
            var oldestForTrend = _weatherHistory.FirstOrDefault(s => s.Timestamp >= tenMinutesAgo) ?? _weatherHistory[0];
            
            var timeSpan = (latest.Timestamp - oldestForTrend.Timestamp).TotalMinutes;

            if (timeSpan > 1.0) // Need at least 1 minute of data
            {
                // Calculate trend per 10 minutes
                float airTempChange = latest.AirTemp - oldestForTrend.AirTemp;
                float trackTempChange = latest.TrackTemp - oldestForTrend.TrackTemp;

                conditions.AirTempTrend = (airTempChange / (float)timeSpan) * 10f;
                conditions.TrackTempTrend = (trackTempChange / (float)timeSpan) * 10f;
                
                // Sanity check: Cap unrealistic rates (track temp can't change >20°C in 10 minutes)
                // Realistic max: ~5-10°C/10min in extreme conditions (rain/sun transitions)
                conditions.AirTempTrend = Math.Clamp(conditions.AirTempTrend, -15f, 15f);
                conditions.TrackTempTrend = Math.Clamp(conditions.TrackTempTrend, -20f, 20f);

                // Detect significant changes
                conditions.IsWeatherChanging = Math.Abs(conditions.TrackTempTrend) > 0.5f;

                if (conditions.IsWeatherChanging)
                {
                    if (conditions.TrackTempTrend > 0)
                        conditions.ChangeDescription = $"Track warming +{conditions.TrackTempTrend:F1}°C/10min";
                    else
                        conditions.ChangeDescription = $"Track cooling {conditions.TrackTempTrend:F1}°C/10min";
                }
            }
        }

        return conditions;
    }

    /// <summary>
    /// Get grip level estimate based on track temperature
    /// FIX: Accurate tire-compound-based grip estimation
    /// IMPORTANT: Lower track temps (20-30°C) often have BETTER grip than very hot temps (45°C+)
    /// Peak grip varies by compound: Slicks ~30-40°C, Street tires ~25-35°C
    /// </summary>
    public string GetGripEstimate(float trackTemp)
    {
        // Tire grip vs track temperature (based on racing physics):
        // - TOO COLD (<15°C): Tires don't reach operating temp, very low grip
        // - COLD (15-20°C): Below optimal, limited grip
        // - OPTIMAL RANGE (20-40°C): Peak grip zone for most compounds
        //   * Street/Road tires: 25-35°C peak
        //   * Racing slicks: 30-40°C peak
        // - HOT (40-50°C): Still good grip but tires starting to overheat
        // - TOO HOT (>50°C): Tire degradation accelerates, grip drops significantly
        
        if (trackTemp < 15)
            return "Very Low (Too Cold - <15°C)";
        else if (trackTemp < 20)
            return "Low (Cold Track - Tires not at temp)";
        else if (trackTemp < 30)
            return "Optimal (Cool - Peak grip 20-30°C)";
        else if (trackTemp < 40)
            return "Optimal (Warm - Peak grip 30-40°C)";
        else if (trackTemp < 50)
            return "Good (Hot - Watch tire temps)";
        else if (trackTemp < 60)
            return "Moderate (Very Hot - Tire deg risk)";
        else
            return "Poor (Extreme Heat - High deg)";
    }

    /// <summary>
    /// Check if rain is likely based on weather patterns
    /// </summary>
    public bool IsRainLikely()
    {
        if (_weatherHistory.Count < 10)
            return false;

        var recent = _weatherHistory.TakeLast(10).ToList();
        
        // Check if sky conditions are deteriorating
        int clearCount = recent.Count(w => w.Skies <= 1); // Clear/Partly Cloudy
        int cloudyCount = recent.Count(w => w.Skies >= 2); // Mostly Cloudy/Overcast

        return cloudyCount > clearCount;
    }

    /// <summary>
    /// Get downforce percentage relative to baseline
    /// Air density affects downforce: higher density = more downforce
    /// </summary>
    public float GetDownforcePercentage(float currentAirDensity, float baselineAirDensity = 1.225f)
    {
        if (baselineAirDensity <= 0)
            baselineAirDensity = 1.225f; // Standard air density at sea level

        return (currentAirDensity / baselineAirDensity) * 100f;
    }

    /// <summary>
    /// Clear weather history (e.g., on new session)
    /// FIX: Also resets baseline temperatures
    /// </summary>
    public void ClearHistory()
    {
        _weatherHistory.Clear();
        _baselineAirTemp = null;
        _baselineTrackTemp = null;
        _baselineSessionId = null;
    }

    /// <summary>
    /// Convert sky condition enum to description
    /// </summary>
    private static string GetSkyDescription(int skies)
    {
        return skies switch
        {
            0 => "Clear",
            1 => "Partly Cloudy",
            2 => "Mostly Cloudy",
            3 => "Overcast",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Convert weather type enum to description
    /// </summary>
    private static string GetWeatherDescription(int weatherType)
    {
        return weatherType switch
        {
            0 => "Constant",
            1 => "Dynamic",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Convert wind direction from radians to cardinal direction (N, NE, E, SE, S, SW, W, NW)
    /// </summary>
    private static string GetWindDirectionCardinal(float radians)
    {
        // Convert radians to degrees (0-360)
        float degrees = (radians * (180f / MathF.PI) + 360f) % 360f;
        
        // 8-point compass with 45° sectors
        if (degrees >= 337.5f || degrees < 22.5f)
            return "N";
        else if (degrees < 67.5f)
            return "NE";
        else if (degrees < 112.5f)
            return "E";
        else if (degrees < 157.5f)
            return "SE";
        else if (degrees < 202.5f)
            return "S";
        else if (degrees < 247.5f)
            return "SW";
        else if (degrees < 292.5f)
            return "W";
        else
            return "NW";
    }
    
    /// <summary>
    /// Convert track wetness enum to description
    /// SDK: 0 = Dry, 1 = MostlyDry, 2 = VeryLightlyWet, 3 = LightlyWet, 
    ///      4 = ModeratelyWet, 5 = VeryWet, 6 = ExtremelyWet
    /// </summary>
    private static string GetTrackWetnessDescription(int wetness)
    {
        return wetness switch
        {
            0 => "Dry",
            1 => "Mostly Dry",
            2 => "Very Lightly Wet",
            3 => "Lightly Wet",
            4 => "Moderately Wet",
            5 => "Very Wet",
            6 => "Extremely Wet",
            _ => "Unknown"
        };
    }
}
