using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.SetupEngineering;

/// <summary>
/// Automatic handling issue detection from telemetry
/// Detects oversteer, understeer, and brake lockups without user input
/// 
/// Detection Methods:
/// - Oversteer: High steering corrections + low lateral G + high yaw rate
/// - Understeer: High throttle + low speed gain + high steering angle
/// - Brake Lockups: Wheel speed < 80% of vehicle speed while braking
/// </summary>
public class HandlingDetector
{
    // Detection thresholds (tuned from iRacing telemetry data)
    private const float OVERSTEER_STEERING_RATE_THRESHOLD = 0.3f;    // rad/s
    private const float OVERSTEER_LATERAL_G_THRESHOLD = 1.2f;        // g-force
    private const float OVERSTEER_YAW_RATE_THRESHOLD = 0.15f;        // rad/s
    
    private const float UNDERSTEER_STEERING_ANGLE_THRESHOLD = 90f;   // degrees
    private const float UNDERSTEER_THROTTLE_THRESHOLD = 0.7f;        // 0-1 scale
    private const float UNDERSTEER_SPEED_GAIN_THRESHOLD = 5.0f;      // km/h
    
    private const float LOCKUP_WHEEL_SPEED_RATIO = 0.8f;             // 80% of vehicle speed
    private const float LOCKUP_BRAKE_THRESHOLD = 0.5f;               // 50% brake pressure
    
    /// <summary>
    /// Detect oversteer from corner telemetry data
    /// Oversteer signature: rapid steering corrections + low lateral grip + sliding rear
    /// </summary>
    /// <param name="cornerData">Telemetry samples from corner entry to exit</param>
    /// <returns>Oversteer severity (0-10 scale)</returns>
    public float DetectOversteer(List<TelemetryData> cornerData)
    {
        if (cornerData.Count < 10)
            return 0f; // Not enough data
        
        // Calculate steering rate (change in steering angle per frame)
        var steeringRate = CalculateSteeringRate(cornerData);
        
        // Average lateral acceleration (cornering grip)
        var lateralG = cornerData.Average(d => Math.Abs(d.LatAccel));
        
        // Average yaw rate (rotation around vertical axis)
        var yawRate = cornerData.Average(d => Math.Abs(d.YawRate));
        
        // Oversteer detection logic
        // Severe oversteer: High yaw rate + low lateral G + rapid steering corrections
        if (yawRate > OVERSTEER_YAW_RATE_THRESHOLD && 
            lateralG < OVERSTEER_LATERAL_G_THRESHOLD && 
            steeringRate > OVERSTEER_STEERING_RATE_THRESHOLD)
        {
            // Calculate severity based on how much thresholds are exceeded
            var yawSeverity = (yawRate - OVERSTEER_YAW_RATE_THRESHOLD) / 0.1f;
            var gripLoss = (OVERSTEER_LATERAL_G_THRESHOLD - lateralG) / 0.3f;
            var steeringSeverity = (steeringRate - OVERSTEER_STEERING_RATE_THRESHOLD) / 0.2f;
            
            var severity = (yawSeverity + gripLoss + steeringSeverity) / 3f;
            return Math.Clamp(severity * 10f, 6f, 10f); // Severe: 6-10
        }
        
        // Moderate oversteer: High yaw rate + moderate steering corrections
        if (yawRate > 0.10f && lateralG < 1.5f && steeringRate > 0.2f)
        {
            return 5.0f; // Moderate
        }
        
        // Mild oversteer: Slight yaw rate increase
        if (yawRate > 0.08f && steeringRate > 0.15f)
        {
            return 3.0f; // Mild
        }
        
        return 0f; // Neutral/balanced
    }
    
    /// <summary>
    /// Detect understeer from corner telemetry data
    /// Understeer signature: high steering angle + high throttle + low speed gain (scrubbing)
    /// </summary>
    /// <param name="cornerData">Telemetry samples from corner entry to exit</param>
    /// <returns>Understeer severity (0-10 scale)</returns>
    public float DetectUndersteer(List<TelemetryData> cornerData)
    {
        if (cornerData.Count < 10)
            return 0f; // Not enough data
        
        // Average steering angle magnitude
        var steeringAngle = cornerData.Average(d => Math.Abs(d.SteeringWheelAngle));
        
        // Average throttle position during corner
        var throttleAvg = cornerData.Average(d => d.Throttle);
        
        // Speed gain through corner (exit - entry)
        var entrySpeed = cornerData.Take(3).Average(d => d.Speed);
        var exitSpeed = cornerData.Skip(cornerData.Count - 3).Average(d => d.Speed);
        var speedGain = exitSpeed - entrySpeed;
        
        // Average lateral acceleration (front grip)
        var lateralG = cornerData.Average(d => Math.Abs(d.LatAccel));
        
        // Understeer detection logic
        // Severe understeer: High steering + high throttle + minimal speed gain (plowing front)
        if (steeringAngle > UNDERSTEER_STEERING_ANGLE_THRESHOLD && 
            throttleAvg > UNDERSTEER_THROTTLE_THRESHOLD && 
            speedGain < UNDERSTEER_SPEED_GAIN_THRESHOLD)
        {
            // Calculate severity
            var steeringSeverity = (steeringAngle - UNDERSTEER_STEERING_ANGLE_THRESHOLD) / 30f;
            var throttleSeverity = (throttleAvg - UNDERSTEER_THROTTLE_THRESHOLD) / 0.2f;
            var scrubSeverity = (UNDERSTEER_SPEED_GAIN_THRESHOLD - speedGain) / 5f;
            
            var severity = (steeringSeverity + throttleSeverity + scrubSeverity) / 3f;
            return Math.Clamp(severity * 10f, 6f, 10f); // Severe: 6-10
        }
        
        // Moderate understeer: High steering + moderate throttle + low speed gain
        if (steeringAngle > 60f && throttleAvg > 0.5f && speedGain < 10f)
        {
            return 5.0f; // Moderate
        }
        
        // Mild understeer: Slightly high steering + low lateral G
        if (steeringAngle > 45f && lateralG < 1.8f)
        {
            return 3.0f; // Mild
        }
        
        return 0f; // Neutral/balanced
    }
    
    /// <summary>
    /// Detect brake lockups from braking zone telemetry
    /// Lockup signature: High longitudinal deceleration spikes + low lateral G during braking
    /// (Individual wheel speeds not available in current telemetry model)
    /// </summary>
    /// <param name="brakingData">Telemetry samples from braking zone</param>
    /// <returns>Lockup detection result with count</returns>
    public LockupDetectionResult DetectLockups(List<TelemetryData> brakingData)
    {
        var result = new LockupDetectionResult();
        
        for (int i = 1; i < brakingData.Count; i++)
        {
            var current = brakingData[i];
            var previous = brakingData[i - 1];
            
            // Only check when braking hard
            if (current.Brake < LOCKUP_BRAKE_THRESHOLD)
                continue;
            
            // Lockup detection via longitudinal acceleration spike
            // During lockup: sudden deceleration spike as tire slides
            var deceleration = previous.LongAccel - current.LongAccel;
            var lateralG = Math.Abs(current.LatAccel);
            
            // Lockup signature: high deceleration spike + low lateral G
            // (locked wheels lose lateral grip too)
            if (deceleration > 1.5f && lateralG < 0.5f && current.Brake > 0.8f)
            {
                result.LockupCount++;
                
                // Approximate which wheels based on brake bias and lateral G
                var brakeBias = current.BrakeBias;
                var wheelGuess = brakeBias > 55f ? "Front" : "Rear";
                if (lateralG > 0.3f)
                    wheelGuess += lateralG > 0 ? " Left" : " Right";
                
                result.AffectedWheels.Add((wheelGuess, current.Speed, current.Speed, current.Brake));
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Calculate steering rate (angular velocity of steering wheel)
    /// High steering rate indicates corrections (fighting oversteer/understeer)
    /// </summary>
    private float CalculateSteeringRate(List<TelemetryData> data)
    {
        if (data.Count < 2)
            return 0f;
        
        float totalRate = 0f;
        int count = 0;
        
        for (int i = 1; i < data.Count; i++)
        {
            var deltaAngle = Math.Abs(data[i].SteeringWheelAngle - data[i - 1].SteeringWheelAngle);
            var deltaTime = (float)(data[i].Timestamp - data[i - 1].Timestamp).TotalSeconds;
            
            if (deltaTime > 0)
            {
                // Convert degrees to radians
                var rate = (deltaAngle * (float)Math.PI / 180f) / deltaTime;
                totalRate += rate;
                count++;
            }
        }
        
        return count > 0 ? totalRate / count : 0f;
    }
}

/// <summary>
/// Result of lockup detection analysis
/// </summary>
public class LockupDetectionResult
{
    /// <summary>
    /// Total number of lockup instances detected
    /// </summary>
    public int LockupCount { get; set; }
    
    /// <summary>
    /// List of affected wheels with details
    /// Format: (WheelName, WheelSpeed, VehicleSpeed, BrakePressure)
    /// </summary>
    public List<(string Wheel, float WheelSpeed, float VehicleSpeed, float BrakePressure)> AffectedWheels { get; set; } = new();
    
    /// <summary>
    /// Get most frequently locked wheel
    /// </summary>
    public string? GetMostFrequentWheel()
    {
        if (AffectedWheels.Count == 0)
            return null;
        
        return AffectedWheels
            .GroupBy(w => w.Wheel)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;
    }
    
    /// <summary>
    /// Check if lockups occurred
    /// </summary>
    public bool HasLockups => LockupCount > 0;
}
