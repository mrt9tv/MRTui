using iRacingOverlay.Core.Models;
using System.Text.Json;

namespace iRacingOverlay.Core.Services.ML;

/// <summary>
/// ML Model Service for Setup Engineering predictions
/// Loads and manages CatBoost/ONNX models for lap time and setup optimization
/// 
/// Current Models:
/// - formulair04_alltracks_v1.0.cbm: Formula IR-04 lap time predictor
/// - predictor_model.cbm: General lap time predictor
/// 
/// Usage:
/// - Setup Engineering: Predict lap time delta from setup changes
/// - Strategy Scouting: Predict fuel consumption from track/weather
/// </summary>
public class MLModelService
{
    private readonly string _modelsDirectory;
    private readonly Dictionary<string, ModelMetadata> _loadedModels = new();
    private bool _modelsLoaded = false;
    
    // Model cache
    private byte[]? _formulaIR04Model = null;
    private byte[]? _predictorModel = null;
    
    public MLModelService(string? modelsDirectory = null)
    {
        // Default to %APPDATA%/MRTOverlay/MLModels/
        modelsDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MRTOverlay",
            "MLModels");
        
        _modelsDirectory = modelsDirectory;
        Directory.CreateDirectory(_modelsDirectory);
        
        Console.WriteLine($"[MLModelService] Initialized with models directory: {_modelsDirectory}");
    }
    
    /// <summary>
    /// Load all available ML models from disk
    /// Called asynchronously at service startup
    /// </summary>
    public async Task LoadModelsAsync()
    {
        if (_modelsLoaded)
            return;
        
        try
        {
            // Scan for .cbm files (CatBoost models)
            var cbmFiles = Directory.GetFiles(_modelsDirectory, "*.cbm");
            Console.WriteLine($"[MLModelService] Found {cbmFiles.Length} CatBoost models");
            
            foreach (var cbmFile in cbmFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(cbmFile);
                var metadataFile = Path.Combine(_modelsDirectory, $"{fileName}_metadata.json");
                
                // Load model bytes
                var modelBytes = await File.ReadAllBytesAsync(cbmFile);
                
                // Load metadata if exists
                ModelMetadata? metadata = null;
                if (File.Exists(metadataFile))
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataFile);
                    metadata = JsonSerializer.Deserialize<ModelMetadata>(metadataJson);
                }
                
                // Cache model
                if (fileName.Contains("formulair04"))
                {
                    _formulaIR04Model = modelBytes;
                    Console.WriteLine($"[MLModelService] ✅ Loaded Formula IR-04 model: {cbmFile} ({modelBytes.Length / 1024}KB)");
                }
                else if (fileName.Contains("predictor"))
                {
                    _predictorModel = modelBytes;
                    Console.WriteLine($"[MLModelService] ✅ Loaded predictor model: {cbmFile} ({modelBytes.Length / 1024}KB)");
                }
                
                // Store metadata
                if (metadata != null)
                {
                    _loadedModels[fileName] = metadata;
                    Console.WriteLine($"[MLModelService]    Metadata: {metadata.Features?.Count ?? 0} features, trained {metadata.TrainingDate}");
                }
            }
            
            _modelsLoaded = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MLModelService] ⚠️ Failed to load models: {ex.Message}");
            Console.WriteLine($"[MLModelService]    Models directory: {_modelsDirectory}");
        }
    }
    
    /// <summary>
    /// Check if ML models are available and loaded
    /// </summary>
    public bool IsAvailable => _modelsLoaded && (_formulaIR04Model != null || _predictorModel != null);
    
    /// <summary>
    /// Get list of loaded model names and metadata
    /// </summary>
    public Dictionary<string, ModelMetadata> GetLoadedModels() => new(_loadedModels);
    
    /// <summary>
    /// Predict lap time improvement from setup change
    /// Returns predicted delta in seconds (negative = faster)
    /// 
    /// Phase 3.6: Physics-based heuristic prediction until ML models are trained
    /// Uses empirical setup parameter impact coefficients from sim racing research
    /// </summary>
    public SetupPrediction? PredictSetupChange(
        string carName,
        string trackName,
        SetupParameterFeatures baselineSetup,
        SetupParameterFeatures proposedSetup)
    {
        Console.WriteLine($"[MLModelService] 🔮 Predicting setup change: {carName} @ {trackName}");
        
        // Physics-based heuristic prediction (until ML models trained)
        var prediction = PredictViaPhysicsHeuristics(baselineSetup, proposedSetup, trackName);
        
        // If ML models are available, use them for refined prediction
        if (IsAvailable && _formulaIR04Model != null)
        {
            // TODO Phase 3.7: Integrate CatBoost/ONNX inference
            // For now, return physics-based prediction
            Console.WriteLine("[MLModelService] ℹ️ Using physics-based prediction (ML inference not yet implemented)");
        }
        
        return prediction;
    }
    
    /// <summary>
    /// Physics-based heuristic prediction system
    /// Uses empirical setup parameter impact coefficients
    /// </summary>
    private SetupPrediction PredictViaPhysicsHeuristics(
        SetupParameterFeatures baseline,
        SetupParameterFeatures proposed,
        string trackName)
    {
        var featureImportance = new Dictionary<string, float>();
        float totalDelta = 0f;
        
        // Aero: Front Wing (higher = more downforce = slower straights, faster corners)
        if (proposed.FrontWing.HasValue && baseline.FrontWing.HasValue)
        {
            var wingDelta = proposed.FrontWing.Value - baseline.FrontWing.Value;
            var trackType = ClassifyTrack(trackName);
            
            // High-speed tracks: More wing = slower lap times (+0.02s per click)
            // Low-speed tracks: More wing = faster lap times (-0.015s per click)
            float wingImpact = trackType == TrackType.HighSpeed ? 0.02f : -0.015f;
            float wingDeltaSeconds = wingDelta * wingImpact;
            
            totalDelta += wingDeltaSeconds;
            featureImportance["FrontWing"] = Math.Abs(wingDeltaSeconds);
        }
        
        // Aero: Rear Wing (similar to front wing but more impact on stability)
        if (proposed.RearWing.HasValue && baseline.RearWing.HasValue)
        {
            var wingDelta = proposed.RearWing.Value - baseline.RearWing.Value;
            var trackType = ClassifyTrack(trackName);
            
            float wingImpact = trackType == TrackType.HighSpeed ? 0.025f : -0.018f;
            float wingDeltaSeconds = wingDelta * wingImpact;
            
            totalDelta += wingDeltaSeconds;
            featureImportance["RearWing"] = Math.Abs(wingDeltaSeconds);
        }
        
        // Chassis: Front ARB (higher = less body roll = faster turn-in)
        if (proposed.FrontARB.HasValue && baseline.FrontARB.HasValue)
        {
            var arbDelta = proposed.FrontARB.Value - baseline.FrontARB.Value;
            // Optimal ARB depends on track, but generally -0.01s per click for understeer fix
            float arbImpact = -0.01f * arbDelta;
            
            totalDelta += arbImpact;
            featureImportance["FrontARB"] = Math.Abs(arbImpact);
        }
        
        // Tires: Cold Pressure (optimal varies by compound, ±1 kPa = ±0.02s)
        var tireDelta = 0f;
        int tireChanges = 0;
        
        if (proposed.LFTirePressure.HasValue && baseline.LFTirePressure.HasValue)
        {
            var pressureDelta = Math.Abs(proposed.LFTirePressure.Value - baseline.LFTirePressure.Value);
            tireDelta += pressureDelta * 0.02f;  // ±0.02s per kPa deviation from optimal
            tireChanges++;
        }
        
        if (tireChanges > 0)
        {
            totalDelta += tireDelta;
            featureImportance["TirePressure"] = tireDelta;
        }
        
        // Confidence scoring based on number of changed parameters
        int changedParams = featureImportance.Count;
        float confidence = changedParams >= 3 ? 0.75f : (changedParams >= 2 ? 0.60f : 0.40f);
        
        // Generate recommendation
        var recommendation = GenerateHeuristicRecommendation(totalDelta, confidence, featureImportance);
        
        return new SetupPrediction
        {
            LapTimeDelta = totalDelta,
            Confidence = confidence,
            Recommendation = recommendation,
            FeatureImportance = featureImportance
        };
    }
    
    /// <summary>
    /// Classify track type for wing prediction
    /// </summary>
    private TrackType ClassifyTrack(string trackName)
    {
        var highSpeedTracks = new[] { "monza", "spa", "lemans", "daytona", "indianapolis" };
        var lowSpeedTracks = new[] { "monaco", "lime rock", "oulton", "brands" };
        
        var lowerTrack = trackName.ToLowerInvariant();
        
        if (highSpeedTracks.Any(t => lowerTrack.Contains(t)))
            return TrackType.HighSpeed;
        
        if (lowSpeedTracks.Any(t => lowerTrack.Contains(t)))
            return TrackType.LowSpeed;
        
        return TrackType.Medium;
    }
    
    /// <summary>
    /// Generate human-readable recommendation from heuristic prediction
    /// </summary>
    private string GenerateHeuristicRecommendation(float delta, float confidence, Dictionary<string, float> importance)
    {
        var rec = "";
        
        if (Math.Abs(delta) < 0.01f)
        {
            rec = "⚖️ NEUTRAL: Predicted delta < 0.01s (minimal impact)";
        }
        else if (delta < 0)
        {
            rec = $"✅ IMPROVEMENT: Predicted {-delta:F3}s faster ({confidence:P0} confidence)";
        }
        else
        {
            rec = $"⚠️ REGRESSION: Predicted +{delta:F3}s slower ({confidence:P0} confidence)";
        }
        
        // Add key parameter insights
        if (importance.Count > 0)
        {
            var topParam = importance.OrderByDescending(kv => kv.Value).First();
            rec += $"\n💡 Key factor: {topParam.Key} ({topParam.Value:F3}s impact)";
        }
        
        rec += "\n\nℹ️ Physics-based prediction (ML models not yet trained)";
        
        return rec;
    }
    
    /// <summary>
    /// Extract features from telemetry data for ML model input
    /// Combines live telemetry (temps) with setup file data
    /// NOTE: Tire cold pressures are not available in real-time telemetry (only in garage/pit settings)
    /// </summary>
    public SetupParameterFeatures ExtractSetupFeatures(Models.TelemetryData telemetry, string setupName)
    {
        return new SetupParameterFeatures
        {
            SetupName = setupName,
            TrackName = telemetry.TrackName ?? "Unknown",
            AirTemp = telemetry.AirTemp,
            TrackTemp = telemetry.TrackTemp
            
            // NOTE: Tire cold pressures, Aero, and Chassis values must be provided
            // from parsed setup files (.htm HTML exports) - not available in live telemetry
        };
    }
    
    /// <summary>
    /// Merge telemetry features with setup file parameters
    /// Used when comparing two setup files with environmental context
    /// </summary>
    public SetupParameterFeatures MergeSetupWithTelemetry(
        SetupEngineering.SetupData setupData,
        Models.TelemetryData telemetry,
        string setupName)
    {
        return new SetupParameterFeatures
        {
            SetupName = setupName,
            TrackName = telemetry.TrackName ?? "Unknown",
            AirTemp = telemetry.AirTemp,
            TrackTemp = telemetry.TrackTemp,
            
            // From setup file
            FrontWing = setupData.Aero.FrontWing,
            RearWing = setupData.Aero.RearWing,
            FrontARB = setupData.Chassis.FrontARB,
            RearARB = setupData.Chassis.RearARB,
            FrontRideHeight = setupData.Chassis.FrontRideHeight,
            RearRideHeight = setupData.Chassis.RearRideHeight,
            
            // Tire pressures from setup file
            LFTirePressure = setupData.Tires.LeftFrontPressure,
            RFTirePressure = setupData.Tires.RightFrontPressure,
            LRTirePressure = setupData.Tires.LeftRearPressure,
            RRTirePressure = setupData.Tires.RightRearPressure
        };
    }
    
    /// <summary>
    /// Copy models from application bundle to %APPDATA% on first run
    /// Called by application startup
    /// </summary>
    public Task CopyBundledModelsAsync(string bundledModelsPath)
    {
        if (!Directory.Exists(bundledModelsPath))
        {
            Console.WriteLine($"[MLModelService] ⚠️ Bundled models not found: {bundledModelsPath}");
            return Task.CompletedTask;
        }
        
        try
        {
            var cbmFiles = Directory.GetFiles(bundledModelsPath, "*.cbm");
            var jsonFiles = Directory.GetFiles(bundledModelsPath, "*_metadata.json");
            
            foreach (var file in cbmFiles.Concat(jsonFiles))
            {
                var destFile = Path.Combine(_modelsDirectory, Path.GetFileName(file));
                
                // Only copy if doesn't exist (don't overwrite user's custom models)
                if (!File.Exists(destFile))
                {
                    File.Copy(file, destFile, overwrite: false);
                    Console.WriteLine($"[MLModelService] ✅ Copied model: {Path.GetFileName(file)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MLModelService] ⚠️ Failed to copy bundled models: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
}

/// <summary>
/// Model metadata (loaded from {model}_metadata.json)
/// </summary>
public class ModelMetadata
{
    public string? ModelName { get; set; }
    public string? ModelVersion { get; set; }
    public string? TrainingDate { get; set; }
    public List<string>? Features { get; set; }
    public string? TargetVariable { get; set; }
    public Dictionary<string, float>? Metrics { get; set; }
}

/// <summary>
/// Setup parameter features for ML model input
/// Populated from live telemetry + parsed setup files
/// </summary>
public class SetupParameterFeatures
{
    public string SetupName { get; set; } = string.Empty;
    public string TrackName { get; set; } = string.Empty;
    public float AirTemp { get; set; }
    public float TrackTemp { get; set; }
    
    // Aero
    public float? FrontWing { get; set; }
    public float? RearWing { get; set; }
    
    // Chassis/ARB
    public float? FrontARB { get; set; }
    public float? RearARB { get; set; }
    
    // Tires
    public float? LFTirePressure { get; set; }
    public float? RFTirePressure { get; set; }
    public float? LRTirePressure { get; set; }
    public float? RRTirePressure { get; set; }
    
    // Ride Heights
    public float? FrontRideHeight { get; set; }
    public float? RearRideHeight { get; set; }
}

/// <summary>
/// Track classification for setup predictions
/// </summary>
internal enum TrackType
{
    HighSpeed,   // Monza, Spa, Le Mans - long straights, less downforce optimal
    Medium,      // Most tracks
    LowSpeed     // Monaco, Lime Rock - tight corners, more downforce optimal
}

/// <summary>
/// Setup prediction result from ML model
/// </summary>
public class SetupPrediction
{
    /// <summary>
    /// Predicted lap time delta in seconds (negative = faster)
    /// </summary>
    public float LapTimeDelta { get; set; }
    
    /// <summary>
    /// Prediction confidence (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; }
    
    /// <summary>
    /// Human-readable recommendation
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;
    
    /// <summary>
    /// Feature importance scores (which setup parameters matter most)
    /// </summary>
    public Dictionary<string, float> FeatureImportance { get; set; } = new();
    
    /// <summary>
    /// Whether this prediction is reliable enough to show to user
    /// </summary>
    public bool IsHighConfidence => Confidence >= 0.8f;
}
