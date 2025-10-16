using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Calculates proximity of cars relative to player using actual distance in meters
/// </summary>
public class ProximityCalculator
{
    // Distance thresholds in METERS for color zones - RACING-TIGHT
    private const float VERY_CLOSE_THRESHOLD = 4f;    // <4m - CRITICAL (blinking red)
    private const float CLOSE_THRESHOLD = 7f;         // 4-7m - WARNING (red)
    private const float NEAR_THRESHOLD = 12f;         // 7-12m - CAUTION (orange)
    private const float CAREFUL_THRESHOLD = 16f;      // 12-16m - CAREFUL (yellow)
    
    // Detection range: Use PERCENTAGE of track, not fixed meters!
    // This works on all track sizes (small ovals to Nordschleife)
    private const float DETECTION_PERCENTAGE = 0.25f;  // Detect cars within 25% of track ahead/behind
    private const float MIN_DETECTION_DISTANCE = 300f; // Minimum 300m even on tiny tracks
    private const float MAX_DETECTION_DISTANCE = 2000f; // Maximum 2000m even on huge tracks
    
    /// <summary>
    /// Calculate relative distance between player and another car in METERS
    /// Uses lap-independent circular track logic - shortest distance around the track
    /// </summary>
    /// <param name="playerPct">Player track position (0.0-1.0)</param>
    /// <param name="carPct">Car track position (0.0-1.0)</param>
    /// <param name="trackLength">Track length in meters</param>
    /// <returns>Relative distance in METERS (positive = ahead, negative = behind)</returns>
    public float CalculateRelativeDistance(float playerPct, float carPct, float trackLength)
    {
        float diff = carPct - playerPct;
        
        // Handle circular track wrap-around - always use SHORTEST path
        // Track is circular: 0% -> 100% -> 0%
        // Example: Player at 1%, car at 99% -> diff = -0.98, but shortest path is +0.02 (car 2% ahead going forward)
        // Example: Player at 99%, car at 1% -> diff = -0.98, but shortest path is +0.02 (car 2% ahead going forward)
        
        if (diff > 0.5f)
        {
            // Car is more than halfway ahead -> Actually closer going backward (behind)
            diff -= 1.0f;  
        }
        else if (diff < -0.5f)
        {
            // Car is more than halfway behind -> Actually closer going forward (ahead)
            diff += 1.0f;  
        }
        
        // Convert percentage to meters
        return diff * trackLength;
    }
    
    /// <summary>
    /// Classify distance into proximity zone (using METERS)
    /// </summary>
    /// <param name="absoluteDistance">Absolute distance in METERS</param>
    /// <returns>Proximity zone classification</returns>
    public ProximityZone ClassifyDistance(float absoluteDistance)
    {
        if (absoluteDistance < VERY_CLOSE_THRESHOLD)
            return ProximityZone.VeryClose;  // <4m - Blinking Red
        
        if (absoluteDistance < CLOSE_THRESHOLD)
            return ProximityZone.Close;      // 4-7m - Red
        
        if (absoluteDistance < NEAR_THRESHOLD)
            return ProximityZone.Near;       // 7-12m - Orange
        
        if (absoluteDistance < CAREFUL_THRESHOLD)
            return ProximityZone.Careful;    // 12-16m - Yellow
        
        // Far zone is anything beyond CAREFUL but still within detection range
        // (detection range is dynamically calculated based on track length)
        return ProximityZone.Far;            // >16m - Green
    }
    
    /// <summary>
    /// Calculate detection range in meters based on track length
    /// Uses percentage of track with min/max bounds
    /// </summary>
    private float GetDetectionRange(float trackLength)
    {
        // Use 25% of track length
        float rangeByPercentage = trackLength * DETECTION_PERCENTAGE;
        
        // Clamp between min and max
        return Math.Clamp(rangeByPercentage, MIN_DETECTION_DISTANCE, MAX_DETECTION_DISTANCE);
    }
    
    /// <summary>
    /// Get all cars within detection range, sorted by distance
    /// </summary>
    /// <param name="data">Telemetry data</param>
    /// <param name="maxCars">Maximum number of cars to return (default 5)</param>
    /// <param name="sameClassOnly">Only return cars in same class as player</param>
    /// <returns>List of proximity info, sorted by absolute distance (closest first)</returns>
    public List<ProximityInfo> GetNearbyCars(
        TelemetryData data,
        int maxCars = 5,
        bool sameClassOnly = false)
    {
        var nearbyCars = new List<ProximityInfo>();
        
        // Validate data
        if (data.CarIdxLapDistPct == null || data.CarIdxLap == null)
        {
            return nearbyCars;
        }
        
        // Get track length - CRITICAL for meter-based distances!
        float trackLength = data.TrackLength;
        if (trackLength <= 0)
        {
            Console.WriteLine($"[PROXIMITY] WARNING: Invalid track length ({trackLength}m), using 1000m default");
            trackLength = 1000f; // Fallback to 1km if track length not available
        }
        
        // Calculate detection range based on track length (25% of track, min 300m, max 2000m)
        float detectionRange = GetDetectionRange(trackLength);
        
        int playerIdx = data.PlayerCarIdx;
        float playerPct = data.CarIdxLapDistPct[playerIdx];
        int playerLap = data.CarIdxLap[playerIdx];
        int playerClass = data.PlayerCarClass;
        
        // DEBUG: Log detection parameters
        bool enableDebug = trackLength > 3000 && trackLength < 3200; // Only log on this specific track
        if (enableDebug)
        {
            Console.WriteLine($"[PROXIMITY_DEBUG] TrackLength={trackLength:F1}m, DetectionRange={detectionRange:F0}m");
            Console.WriteLine($"[PROXIMITY_DEBUG] Player: Idx={playerIdx}, Pct={playerPct:F4}, Lap={playerLap}");
        }
        
        int carsScanned = 0;
        int carsSkippedInvalid = 0;
        int carsSkippedPit = 0;
        int carsSkippedDistance = 0;
        
        // Scan all cars
        for (int carIdx = 0; carIdx < data.CarIdxLapDistPct.Length; carIdx++)
        {
            // Skip player
            if (carIdx == playerIdx)
                continue;
            
            float carPct = data.CarIdxLapDistPct[carIdx];
            
            // Skip invalid positions (car not on track)
            if (carPct < 0 || carPct > 1.0f)
            {
                carsSkippedInvalid++;
                continue;
            }
            
            // Skip cars on pit road if data available
            if (data.CarIdxOnPitRoad?[carIdx] == true)
            {
                carsSkippedPit++;
                continue;
            }
            
            int carLap = data.CarIdxLap?[carIdx] ?? 0;
            int carClass = data.CarIdxClass?[carIdx] ?? 0;
            
            // Filter by class if requested
            if (sameClassOnly && carClass != playerClass)
                continue;
            
            carsScanned++;
            
            // Calculate relative distance in METERS (LAP-INDEPENDENT - uses circular track logic)
            float relativeDistance = CalculateRelativeDistance(playerPct, carPct, trackLength);
            float absoluteDistance = Math.Abs(relativeDistance);
            
            if (enableDebug && carsScanned <= 5)
            {
                Console.WriteLine($"[PROXIMITY_DEBUG] Car#{carIdx}: Pct={carPct:F4}, Lap={carLap}, RelDist={relativeDistance:F0}m, AbsDist={absoluteDistance:F0}m vs Range={detectionRange:F0}m");
            }
            
            // Skip cars outside detection range (dynamic based on track length)
            if (absoluteDistance > detectionRange)
            {
                carsSkippedDistance++;
                continue;
            }
            
            // Create proximity info
            var proximityInfo = new ProximityInfo
            {
                CarIdx = carIdx,
                RelativeDistance = relativeDistance,
                AbsoluteDistance = absoluteDistance,
                Position = data.CarIdxPosition?[carIdx] ?? 0,
                ClassPosition = data.CarIdxClassPosition?[carIdx] ?? 0,
                CarClass = carClass,
                Lap = carLap,
                OnPitRoad = data.CarIdxOnPitRoad?[carIdx] ?? false,
                Zone = ClassifyDistance(absoluteDistance)
            };
            
            nearbyCars.Add(proximityInfo);
        }
        
        // Sort by absolute distance (closest first)
        nearbyCars.Sort((a, b) => a.AbsoluteDistance.CompareTo(b.AbsoluteDistance));
        
        // DEBUG: Log summary
        if (enableDebug)
        {
            Console.WriteLine($"[PROXIMITY_DEBUG] SUMMARY: Scanned={carsScanned}, Found={nearbyCars.Count}, SkippedInvalid={carsSkippedInvalid}, SkippedPit={carsSkippedPit}, SkippedDistance={carsSkippedDistance}");
        }
        
        // Return top N cars
        return nearbyCars.Take(maxCars).ToList();
    }
    
    /// <summary>
    /// Get closest car ahead of player
    /// </summary>
    public ProximityInfo? GetClosestCarAhead(TelemetryData data, bool sameClassOnly = false)
    {
        var nearbyCars = GetNearbyCars(data, maxCars: 10, sameClassOnly);
        
        // Filter for cars ahead (RelativeDistance > 0 means car is ahead in meters)
        var carsAhead = nearbyCars.Where(c => c.RelativeDistance > 0).ToList();
        
        // Return closest car ahead (already sorted by absolute distance)
        return carsAhead.FirstOrDefault();
    }
    
    /// <summary>
    /// Get closest car behind player
    /// </summary>
    public ProximityInfo? GetClosestCarBehind(TelemetryData data, bool sameClassOnly = false)
    {
        var nearbyCars = GetNearbyCars(data, maxCars: 10, sameClassOnly);
        
        // Filter for cars behind (RelativeDistance < 0 means car is behind in meters)
        var carsBehind = nearbyCars.Where(c => c.RelativeDistance < 0).ToList();
        
        // Return closest car behind (already sorted by absolute distance)
        return carsBehind.FirstOrDefault();
    }
    
    /// <summary>
    /// Get proximity zone for front radar (closest car ahead)
    /// </summary>
    public ProximityZone GetFrontZone(TelemetryData data)
    {
        // Get all nearby cars and manually find closest ahead
        var nearbyCars = GetNearbyCars(data, maxCars: 10, sameClassOnly: false);
        
        // Find closest car ahead (positive RelativeDistance, smallest AbsoluteDistance)
        var carAhead = nearbyCars
            .Where(c => c.RelativeDistance > 0)  // Ahead in meters
            .OrderBy(c => c.AbsoluteDistance)     // Closest first
            .FirstOrDefault();
        
        return carAhead?.Zone ?? ProximityZone.Clear;
    }
    
    /// <summary>
    /// Get proximity zone for rear radar (closest car behind)
    /// </summary>
    public ProximityZone GetRearZone(TelemetryData data)
    {
        // Get all nearby cars and manually find closest behind
        var nearbyCars = GetNearbyCars(data, maxCars: 10, sameClassOnly: false);
        
        // Find closest car behind (negative RelativeDistance, smallest AbsoluteDistance)
        var carBehind = nearbyCars
            .Where(c => c.RelativeDistance < 0)   // Behind in meters
            .OrderBy(c => c.AbsoluteDistance)      // Closest first
            .FirstOrDefault();
        
        return carBehind?.Zone ?? ProximityZone.Clear;
    }
}
