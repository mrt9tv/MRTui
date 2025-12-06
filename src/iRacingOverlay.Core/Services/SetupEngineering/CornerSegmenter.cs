using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services.SetupEngineering;

/// <summary>
/// Automatic corner segmentation from telemetry
/// Identifies corners without manual track mapping
/// 
/// Detection Method:
/// - Corner = lateral acceleration > 0.5g OR steering angle > 30°
/// - Segments lap into corner/straight sections
/// - Analyzes handling issues per corner
/// </summary>
public class CornerSegmenter
{
    // Thresholds for corner detection
    private const float LATERAL_G_THRESHOLD = 0.5f;       // 0.5g lateral acceleration
    private const float STEERING_ANGLE_THRESHOLD = 30f;   // 30° steering angle
    private const int MIN_CORNER_SAMPLES = 10;            // Minimum 10 samples for valid corner
    
    /// <summary>
    /// Segment lap telemetry into corners
    /// Returns list of corner segments with entry/apex/exit data
    /// </summary>
    /// <param name="lapData">Full lap telemetry (60Hz)</param>
    /// <returns>List of detected corners</returns>
    public List<CornerSegment> SegmentCorners(List<TelemetryData> lapData)
    {
        if (lapData.Count == 0)
            return new List<CornerSegment>();
        
        var corners = new List<CornerSegment>();
        var inCorner = false;
        var cornerStart = 0;
        var cornerNumber = 1;
        
        for (int i = 0; i < lapData.Count; i++)
        {
            var data = lapData[i];
            
            // Corner detection: lateral G or steering angle
            var isCornerFrame = Math.Abs(data.LatAccel) > LATERAL_G_THRESHOLD || 
                                Math.Abs(data.SteeringWheelAngle) > STEERING_ANGLE_THRESHOLD;
            
            if (!inCorner && isCornerFrame)
            {
                // Corner entry detected
                inCorner = true;
                cornerStart = i;
            }
            else if (inCorner && !isCornerFrame)
            {
                // Corner exit detected
                inCorner = false;
                
                var cornerLength = i - cornerStart;
                
                // Only count corners with sufficient data
                if (cornerLength >= MIN_CORNER_SAMPLES)
                {
                    var cornerData = lapData.GetRange(cornerStart, cornerLength);
                    var segment = AnalyzeCorner(cornerNumber++, cornerData, cornerStart);
                    corners.Add(segment);
                }
            }
        }
        
        // Handle case where lap ends mid-corner
        if (inCorner && lapData.Count - cornerStart >= MIN_CORNER_SAMPLES)
        {
            var cornerData = lapData.GetRange(cornerStart, lapData.Count - cornerStart);
            var segment = AnalyzeCorner(cornerNumber, cornerData, cornerStart);
            corners.Add(segment);
        }
        
        return corners;
    }
    
    /// <summary>
    /// Analyze corner segment for performance metrics
    /// Identifies entry/apex/exit points and calculates speeds
    /// </summary>
    private CornerSegment AnalyzeCorner(int cornerNumber, List<TelemetryData> cornerData, int startIndex)
    {
        // Find apex (slowest point in corner)
        var apexIndex = 0;
        var minSpeed = float.MaxValue;
        
        for (int i = 0; i < cornerData.Count; i++)
        {
            if (cornerData[i].Speed < minSpeed)
            {
                minSpeed = cornerData[i].Speed;
                apexIndex = i;
            }
        }
        
        // Entry = first 3 samples
        var entryData = cornerData.Take(Math.Min(3, cornerData.Count)).ToList();
        var entrySpeed = entryData.Average(d => d.Speed);
        
        // Apex = 3 samples around minimum speed
        var apexStart = Math.Max(0, apexIndex - 1);
        var apexEnd = Math.Min(cornerData.Count, apexIndex + 2);
        var apexData = cornerData.GetRange(apexStart, apexEnd - apexStart);
        var apexSpeed = apexData.Average(d => d.Speed);
        
        // Exit = last 3 samples
        var exitData = cornerData.Skip(Math.Max(0, cornerData.Count - 3)).ToList();
        var exitSpeed = exitData.Average(d => d.Speed);
        
        // Calculate average metrics
        var avgLateralG = cornerData.Average(d => Math.Abs(d.LatAccel));
        var avgSteeringAngle = cornerData.Average(d => Math.Abs(d.SteeringWheelAngle));
        var avgThrottle = cornerData.Average(d => d.Throttle);
        var avgBrake = cornerData.Average(d => d.Brake);
        
        return new CornerSegment
        {
            CornerNumber = cornerNumber,
            StartIndex = startIndex,
            EndIndex = startIndex + cornerData.Count,
            Data = cornerData,
            
            // Entry/Apex/Exit speeds
            EntrySpeed = entrySpeed,
            ApexSpeed = apexSpeed,
            ExitSpeed = exitSpeed,
            SpeedGain = exitSpeed - entrySpeed,
            
            // Handling metrics
            AverageLateralG = avgLateralG,
            AverageSteeringAngle = avgSteeringAngle,
            AverageThrottle = avgThrottle,
            AverageBrake = avgBrake,
            
            // Performance analysis
            Duration = (float)(cornerData.Last().Timestamp - cornerData.First().Timestamp).TotalSeconds
        };
    }
    
    /// <summary>
    /// Analyze all corners in a lap and detect handling issues
    /// Uses HandlingDetector to classify oversteer/understeer per corner
    /// </summary>
    public List<CornerAnalysis> AnalyzeCornerHandling(List<CornerSegment> corners, HandlingDetector detector)
    {
        var analyses = new List<CornerAnalysis>();
        
        foreach (var corner in corners)
        {
            // Detect oversteer and understeer for this corner
            var oversteerSeverity = detector.DetectOversteer(corner.Data);
            var understeerSeverity = detector.DetectUndersteer(corner.Data);
            
            // Determine primary issue (higher severity wins)
            string issueType = "Neutral";
            float severity = 0f;
            
            if (oversteerSeverity > understeerSeverity && oversteerSeverity >= 3f)
            {
                issueType = "Oversteer";
                severity = oversteerSeverity;
            }
            else if (understeerSeverity > oversteerSeverity && understeerSeverity >= 3f)
            {
                issueType = "Understeer";
                severity = understeerSeverity;
            }
            
            // Calculate time lost (approximate - would need optimal reference lap)
            // For now, use speed gain as proxy
            var timeLost = corner.SpeedGain < 0 ? Math.Abs(corner.SpeedGain) / 10f : 0f;
            
            analyses.Add(new CornerAnalysis
            {
                CornerNumber = corner.CornerNumber,
                IssueType = issueType,
                Severity = severity,
                TimeLost = timeLost,
                OversteerSeverity = oversteerSeverity,
                UndersteerSeverity = understeerSeverity,
                EntrySpeed = corner.EntrySpeed,
                ApexSpeed = corner.ApexSpeed,
                ExitSpeed = corner.ExitSpeed
            });
        }
        
        return analyses;
    }
}

/// <summary>
/// Represents a corner segment in a lap
/// Contains telemetry data and performance metrics
/// </summary>
public class CornerSegment
{
    /// <summary>
    /// Corner number (1, 2, 3, etc.)
    /// </summary>
    public int CornerNumber { get; set; }
    
    /// <summary>
    /// Start index in lap telemetry array
    /// </summary>
    public int StartIndex { get; set; }
    
    /// <summary>
    /// End index in lap telemetry array
    /// </summary>
    public int EndIndex { get; set; }
    
    /// <summary>
    /// Telemetry data for this corner
    /// </summary>
    public List<TelemetryData> Data { get; set; } = new();
    
    /// <summary>
    /// Corner entry speed (km/h)
    /// </summary>
    public float EntrySpeed { get; set; }
    
    /// <summary>
    /// Corner apex speed (km/h)
    /// </summary>
    public float ApexSpeed { get; set; }
    
    /// <summary>
    /// Corner exit speed (km/h)
    /// </summary>
    public float ExitSpeed { get; set; }
    
    /// <summary>
    /// Speed gained through corner (exit - entry)
    /// Negative = losing speed (braking corner)
    /// </summary>
    public float SpeedGain { get; set; }
    
    /// <summary>
    /// Average lateral G through corner
    /// </summary>
    public float AverageLateralG { get; set; }
    
    /// <summary>
    /// Average steering angle through corner (degrees)
    /// </summary>
    public float AverageSteeringAngle { get; set; }
    
    /// <summary>
    /// Average throttle position (0-1)
    /// </summary>
    public float AverageThrottle { get; set; }
    
    /// <summary>
    /// Average brake pressure (0-1)
    /// </summary>
    public float AverageBrake { get; set; }
    
    /// <summary>
    /// Corner duration (seconds)
    /// </summary>
    public float Duration { get; set; }
}

/// <summary>
/// Analysis of handling issues in a specific corner
/// </summary>
public class CornerAnalysis
{
    /// <summary>
    /// Corner number
    /// </summary>
    public int CornerNumber { get; set; }
    
    /// <summary>
    /// Estimated time lost in this corner (seconds)
    /// </summary>
    public float TimeLost { get; set; }
    
    /// <summary>
    /// Primary issue type: "Oversteer", "Understeer", "Lockup", "Neutral"
    /// </summary>
    public string IssueType { get; set; } = "Neutral";
    
    /// <summary>
    /// Issue severity (0-10 scale)
    /// </summary>
    public float Severity { get; set; }
    
    /// <summary>
    /// Oversteer severity (0-10)
    /// </summary>
    public float OversteerSeverity { get; set; }
    
    /// <summary>
    /// Understeer severity (0-10)
    /// </summary>
    public float UndersteerSeverity { get; set; }
    
    /// <summary>
    /// Corner entry speed (km/h)
    /// </summary>
    public float EntrySpeed { get; set; }
    
    /// <summary>
    /// Corner apex speed (km/h)
    /// </summary>
    public float ApexSpeed { get; set; }
    
    /// <summary>
    /// Corner exit speed (km/h)
    /// </summary>
    public float ExitSpeed { get; set; }
}
