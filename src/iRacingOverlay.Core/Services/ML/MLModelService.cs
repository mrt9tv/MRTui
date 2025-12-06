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
    /// NOTE: Requires CatBoostNet NuGet package for actual inference
    /// Currently returns placeholder until Phase 3.3 ML Integration
    /// </summary>
    public SetupPrediction? PredictSetupChange(
        string carName,
        string trackName,
        SetupParameterFeatures baselineSetup,
        SetupParameterFeatures proposedSetup)
    {
        if (!IsAvailable)
        {
            Console.WriteLine("[MLModelService] ⚠️ Models not loaded, cannot predict");
            return null;
        }
        
        // TODO Phase 3.3: Implement CatBoost inference with CatBoostNet
        // For now, return placeholder result
        
        Console.WriteLine($"[MLModelService] 🔮 Predicting setup change: {carName} @ {trackName}");
        
        // Placeholder: Return mock prediction until Phase 3.3
        return new SetupPrediction
        {
            LapTimeDelta = -0.15f,  // Mock: -0.15s faster
            Confidence = 0.0f,      // 0% confidence (placeholder)
            Recommendation = "⚠️ ML predictions not yet implemented (Phase 3.3 pending)",
            FeatureImportance = new Dictionary<string, float>
            {
                { "FrontWing", 0.35f },
                { "RearWing", 0.28f },
                { "FrontARB", 0.22f }
            }
        };
    }
    
    /// <summary>
    /// Extract features from telemetry data for ML model input
    /// NOTE: Actual setup parameter extraction requires .sto file parsing (Phase 3.3)
    /// </summary>
    public SetupParameterFeatures ExtractSetupFeatures(TelemetryData telemetry, string setupName)
    {
        // Extract available telemetry data
        // NOTE: Full setup parsing requires iRacing setup file (.sto) parsing
        return new SetupParameterFeatures
        {
            SetupName = setupName,
            TrackName = telemetry.TrackName,
            AirTemp = telemetry.AirTemp,
            TrackTemp = telemetry.TrackTemp
            
            // TODO Phase 3.3: Extract tire pressures from telemetry
            // TODO Phase 3.3: Parse .sto file for suspension values
            // FrontWing, RearWing, FrontARB, RearARB, etc.
        };
    }
    
    /// <summary>
    /// Copy models from application bundle to %APPDATA% on first run
    /// Called by application startup
    /// </summary>
    public async Task CopyBundledModelsAsync(string bundledModelsPath)
    {
        if (!Directory.Exists(bundledModelsPath))
        {
            Console.WriteLine($"[MLModelService] ⚠️ Bundled models not found: {bundledModelsPath}");
            return;
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
/// Represents actual car setup parameters (from .sto file)
/// TODO Phase 3.3: Populate from .sto file parsing
/// </summary>
public class SetupParameterFeatures
{
    public string SetupName { get; set; } = string.Empty;
    public string TrackName { get; set; } = string.Empty;
    public float AirTemp { get; set; }
    public float TrackTemp { get; set; }
    
    // TODO Phase 3.3: Add setup parameters from .sto file parsing
    // public float LFTirePressure { get; set; }
    // public float FrontWing { get; set; }
    // public float RearWing { get; set; }
    // public float FrontARB { get; set; }
    // public float RearARB { get; set; }
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
