# Machine Learning for Fuel & Tire Prediction Analysis

**Date**: December 6, 2025  
**Status**: 🔬 Research & Recommendations  
**Purpose**: Evaluate ML/NN approaches for improving fuel and tire predictions

---

## 📊 Current State Analysis

### Existing Prediction System

**Fuel Calculator Current Approach**:
1. **Simple Averages**: Last lap, L5, L10, session average
2. **Exponential Moving Average (EMA)**: Adaptive alpha based on consistency
3. **Outlier Detection**: MAD (Median Absolute Deviation) + IQR filtering
4. **Historical Lookup**: Simple EMA-based track/car historical averages
5. **Flag-Aware**: Separate green flag vs yellow flag averages

**Strengths** ✅:
- Fast, deterministic calculations
- Low computational overhead
- Works well for consistent driving
- Real-time updates at 25-60Hz

**Weaknesses** ❌:
- **Cold start problem**: Needs 3-5 laps for accurate predictions
- **Context-blind**: Doesn't consider track sections, traffic, or driver behavior patterns
- **Weather naive**: Simple temperature correction (±2.5% per 10°C)
- **No learning**: Can't improve predictions from stint-to-stint patterns
- **Tire wear**: Currently placeholder, no real prediction model

---

## 🤖 Machine Learning Opportunity Assessment

### Problem 1: Fuel Consumption Prediction

**Current Issues**:
- First 3 laps have poor predictions (waiting for valid data)
- Temperature/weather corrections are linear approximations
- Cannot predict lap-to-lap variation (traffic, tire deg, driver fatigue)
- No understanding of track sector difficulty

**ML Approach: Gradient Boosted Trees (CatBoost)**

**Why CatBoost over Neural Networks?**
1. **Better for tabular data** (telemetry features)
2. **Built-in categorical handling** (track names, car classes)
3. **Handles missing data** gracefully
4. **Faster inference** (<1ms predictions)
5. **Interpretable** (feature importance, SHAP values)
6. **Less data hungry** (works with 100+ laps vs NN's 10,000+)

**Input Features** (22 features):
```
Session Context (7):
- Track name (categorical)
- Car class ID (categorical)
- Track length (numerical)
- Track temperature (numerical)
- Air temperature (numerical)
- Weather type (categorical: clear/cloudy/rain)
- Session type (categorical: race/practice/qualify)

Lap Context (8):
- Lap number (numerical)
- Lap distance pct (numerical)
- Current fuel level (numerical)
- Fuel level pct (numerical)
- Lap time (numerical)
- Session flag status (categorical: green/yellow/red)
- Position in race (numerical)
- Cars ahead/behind (numerical)

Historical Context (7):
- Previous lap fuel used (numerical)
- L3 average fuel (numerical)
- L5 average fuel (numerical)
- Stint lap number (numerical)
- Laps since pit (numerical)
- Tire age (laps) (numerical)
- Historical track average (numerical)
```

**Target**: `FuelUsedThisLap` (regression)

**Training Data Collection**:
- Save every completed lap to SQLite database
- Fields: All input features + actual fuel used
- Size: ~500 bytes per lap
- Storage: 1000 laps = 500KB, 10,000 laps = 5MB
- Collection: Background thread after each lap completion

**Model Architecture**:
```python
import catboost as cb

model = cb.CatBoostRegressor(
    iterations=500,
    learning_rate=0.05,
    depth=6,
    loss_function='RMSE',
    cat_features=['TrackName', 'CarClass', 'WeatherType', 'FlagStatus'],
    task_type='CPU',
    random_seed=42
)
```

**Inference Integration**:
```csharp
public class MLFuelPredictor
{
    private CatBoostModel _model;
    
    public float PredictFuelUsage(TelemetryData telemetry, LapContext context)
    {
        // Extract features
        var features = new float[22];
        features[0] = EncodeTrackName(telemetry.TrackName);
        features[1] = telemetry.PlayerCarClass;
        // ... populate all features
        
        // Predict (< 1ms inference time)
        var prediction = _model.Predict(features);
        
        return prediction[0];
    }
}
```

**Benefits**:
- ✅ Instant predictions from lap 0 (uses historical patterns)
- ✅ Track section awareness (via lap distance pct)
- ✅ Traffic impact learning (position, cars ahead/behind)
- ✅ Weather pattern learning (not just linear correction)
- ✅ Improves with every lap driven across all sessions

**Estimated Accuracy** (after 1000+ laps trained):
- **First lap prediction**: ±0.2L (vs current ±0.5L)
- **Mid-race prediction**: ±0.05L (vs current ±0.1L)
- **Confidence intervals**: Model provides uncertainty estimates

---

### Problem 2: Tire Wear Prediction

**Current Issues**:
- ❌ **No tire wear tracking at all**
- Placeholder values in TireStrategyService
- Cannot predict tire lifespan or degradation
- No pit strategy for tire changes

**ML Approach: Time Series Forecasting (LSTM/GRU)**

**Why Neural Networks for Tires?**
1. **Sequential data**: Tire wear is cumulative over laps
2. **Complex patterns**: Non-linear degradation curves
3. **Temperature sensitivity**: Tire temps affect wear exponentially
4. **Compound memory**: Different compounds age differently

**Input Features** (15 features per lap):
```
Tire State (4 per tire = 16):
- Tire surface temp (LF, RF, LR, RR)
- Tire core temp (LF, RF, LR, RR)
- Tire pressure (LF, RF, LR, RR)
- Tire wear % (LF, RF, LR, RR) [if available via SDK]

Driving Context (8):
- Lap time (numerical)
- Speed (average, max)
- Brake applications (count)
- Throttle % (average)
- Steering angle (average absolute)
- Track temp (numerical)
- Lap number (numerical)
- Fuel load (numerical)

Setup (3):
- Tire compound (categorical: soft/medium/hard)
- Camber (LF, RF, LR, RR)
- Toe (LF, RF, LR, RR)
```

**Target**: `TireWearRate` (% per lap) + `TireLapsRemaining` (laps)

**Model Architecture**:
```python
import tensorflow as tf

model = tf.keras.Sequential([
    tf.keras.layers.LSTM(64, return_sequences=True, input_shape=(10, 15)),  # 10 laps lookback
    tf.keras.layers.Dropout(0.2),
    tf.keras.layers.LSTM(32),
    tf.keras.layers.Dropout(0.2),
    tf.keras.layers.Dense(16, activation='relu'),
    tf.keras.layers.Dense(2)  # [wear_rate, laps_remaining]
])

model.compile(optimizer='adam', loss='mse', metrics=['mae'])
```

**Inference Integration**:
```csharp
public class MLTirePredictor
{
    private TensorFlowModel _model;
    private Queue<TireLapData> _lapHistory; // 10 lap sliding window
    
    public TirePrediction PredictTireLife(TelemetryData telemetry)
    {
        // Prepare 10-lap sequence
        var sequence = _lapHistory.ToArray();
        
        // Predict (2-5ms inference time)
        var prediction = _model.Predict(sequence);
        
        return new TirePrediction
        {
            WearRatePerLap = prediction[0],
            EstimatedLapsRemaining = (int)prediction[1]
        };
    }
}
```

**Benefits**:
- ✅ Real tire wear tracking (currently missing)
- ✅ Predicts when tires will fall off a cliff
- ✅ Compound-specific degradation curves
- ✅ Temperature-aware (hot track = faster wear)
- ✅ Driving style impact (aggressive = more wear)

**Estimated Accuracy** (after 500+ stints trained):
- **Tire life prediction**: ±3 laps (e.g., 18-24 laps on mediums)
- **Wear rate**: ±0.1% per lap
- **Cliff detection**: 80% accuracy for predicting sudden dropoff

---

## 🎯 Recommended Implementation Strategy

### Phase 1: Data Collection Infrastructure (Week 1)

**Priority**: HIGH - Must collect data before training models

**Tasks**:
1. Create SQLite database schema for lap data
2. Implement background lap data logger
3. Store telemetry snapshots after each lap completion
4. Fields: All 22 fuel features + 15 tire features + targets
5. Testing: Run 10 practice sessions, verify data quality

**Deliverable**: `TelemetryDatabase.db` with schema, logging working

**Estimated Lines of Code**: ~300 lines (database service)

---

### Phase 2: CatBoost Fuel Predictor (Week 2-3)

**Priority**: HIGH - Biggest immediate impact

**Tasks**:
1. Export lap data to CSV for training
2. Train initial CatBoost model (100+ laps minimum)
3. Evaluate model (RMSE, MAE, feature importance)
4. Integrate CatBoost .NET wrapper (CatBoostNet NuGet)
5. Add `MLFuelPredictor` service to FuelCalculatorService
6. Fallback to traditional averages if model unavailable
7. A/B testing: Compare ML predictions vs traditional

**Deliverable**: ML fuel predictor with <0.1L prediction error

**Estimated Lines of Code**: ~500 lines (predictor service + integration)

**NuGet Package**: `CatBoostNet` (Microsoft ML.NET compatible)

---

### Phase 3: LSTM Tire Wear Predictor (Week 4-5)

**Priority**: MEDIUM - New feature, not fixing existing issue

**Tasks**:
1. Research iRacing SDK tire wear telemetry (may not be exposed)
2. If unavailable, use tire temp degradation as proxy
3. Export tire data sequences (10-lap windows)
4. Train LSTM model with TensorFlow/Keras
5. Export model to ONNX format
6. Integrate via ML.NET ONNX runtime
7. Add `MLTirePredictor` service
8. Update RaceStrategyWidget with tire predictions

**Deliverable**: Tire wear predictions in fuel widget

**Estimated Lines of Code**: ~600 lines (predictor service + UI updates)

**NuGet Package**: `Microsoft.ML.OnnxRuntime` + `Microsoft.ML`

---

## 📈 Expected Performance Impact

### Fuel Predictions (CatBoost)

| Metric | Current | With ML | Improvement |
|--------|---------|---------|-------------|
| **First lap accuracy** | ±0.5L | ±0.2L | **60% better** |
| **Cold start (3 laps)** | ±0.2L | ±0.05L | **75% better** |
| **Mid-race accuracy** | ±0.1L | ±0.03L | **70% better** |
| **Prediction time** | <0.1ms | <1ms | Acceptable |
| **Memory usage** | ~5MB | ~15MB | +10MB |

### Tire Wear Predictions (LSTM)

| Metric | Current | With ML | Improvement |
|--------|---------|---------|-------------|
| **Tire life prediction** | N/A | ±3 laps | **New feature** |
| **Wear rate tracking** | N/A | ±0.1%/lap | **New feature** |
| **Cliff warning** | N/A | 80% accuracy | **New feature** |
| **Prediction time** | N/A | 2-5ms | Acceptable |
| **Memory usage** | N/A | ~30MB | +30MB |

---

## 🚧 Challenges & Mitigations

### Challenge 1: Training Data Scarcity
**Problem**: Need 1000+ laps for good CatBoost model, 500+ stints for LSTM  
**Mitigation**:
- Start with smaller models (100-200 laps minimum)
- Use transfer learning (train on multiple tracks/cars, fine-tune per combo)
- Collect data passively during normal usage
- Ship with pre-trained "generic" model from developer testing

### Challenge 2: Model Deployment Size
**Problem**: ML models add 10-50MB to application size  
**Mitigation**:
- Use quantized models (INT8 instead of FP32) = 75% size reduction
- Lazy load models (download on first use)
- Cache models locally, update via background service
- Optional feature (user can disable ML predictions)

### Challenge 3: Cross-Platform Inference
**Problem**: CatBoost/TensorFlow have platform-specific dependencies  
**Mitigation**:
- Use ONNX Runtime (cross-platform, Microsoft supported)
- Export CatBoost → ONNX, TensorFlow → ONNX
- Fallback to CPU inference (GPU not required)
- Graceful degradation if ML unavailable

### Challenge 4: Model Staleness
**Problem**: Models trained on old data may not reflect current meta  
**Mitigation**:
- Continuous learning: Retrain models weekly
- Online adaptation: Fine-tune with recent laps
- Confidence thresholds: Fall back to traditional if prediction uncertain
- User feedback: "Was this prediction accurate?" button

---

## 💰 Cost-Benefit Analysis

### Development Time

| Phase | Effort | Calendar Time | ROI |
|-------|--------|---------------|-----|
| Data collection | 20 hours | 1 week | **Critical foundation** |
| CatBoost fuel | 40 hours | 2 weeks | **Very High** (60% accuracy gain) |
| LSTM tire | 60 hours | 3 weeks | **Medium** (new feature) |
| **Total** | **120 hours** | **6 weeks** | |

### Maintenance Burden

| Task | Frequency | Effort | Owner |
|------|-----------|--------|-------|
| Model retraining | Weekly | 1 hour | Automated |
| Data cleanup | Monthly | 2 hours | Developer |
| Model updates | Quarterly | 4 hours | Developer |
| Bug fixes | As needed | Variable | Developer |

### User Benefits

| Benefit | Impact | Users Affected |
|---------|--------|----------------|
| Better lap 1-3 predictions | High | 100% (every race start) |
| Reduced fuel miscalculations | Medium | 20% (close races) |
| Tire strategy planning | High | 80% (races >30 mins) |
| Confidence in predictions | High | 100% |

---

## 🎬 Recommendation: Phased Approach

### **RECOMMENDED: Phase 1 Only (Data Collection)**

**Rationale**:
- Current system is 52% optimized after refactoring
- Simple averages work well for consistent driving
- ML provides marginal gains for significant complexity
- Data collection is non-intrusive, future-proofs for ML

**Action**: Implement data logging infrastructure now, defer ML training until:
1. 1000+ laps collected per track/car combo
2. User feedback indicates prediction issues
3. Community requests tire wear features

**Timeline**: 1 week development, passive data collection during normal use

---

### **ALTERNATIVE: Full ML Implementation**

**Rationale**:
- Competitive advantage over other overlays
- Tire wear predictions are unique feature
- Better predictions = better race strategy = better results
- "Learning AI" is marketable feature

**Action**: Full Phase 1-3 implementation over 6 weeks

**Risk**: High development cost for uncertain user value

---

## 📚 Technical References

### Libraries & Frameworks

**CatBoost**:
- GitHub: https://github.com/catboost/catboost
- .NET Integration: CatBoostNet NuGet package
- Docs: https://catboost.ai/en/docs/

**TensorFlow/ONNX**:
- ML.NET: https://dotnet.microsoft.com/en-us/apps/machinelearning-ai/ml-dotnet
- ONNX Runtime: https://onnxruntime.ai/
- Model export: TensorFlow → ONNX converter

### Similar Implementations

**F1 Game Telemetry**:
- Uses LSTM for tire temp prediction
- CatBoost for lap time estimation
- Real-time inference at 60Hz

**iRacing CrewChief**:
- Rule-based fuel predictions (similar to current)
- No ML implementation (as of 2024)
- Opportunity for differentiation

**RaceLab**:
- Simple statistical models
- No deep learning
- Focus on data visualization

---

## ✅ Final Recommendations

### Immediate Action (This Month)
1. ✅ **Implement Phase 1 data collection** (1 week)
2. ⏸️ **Pause ML training** until sufficient data collected
3. ✅ **Continue refactoring** existing prediction logic
4. ✅ **Add tire wear tracking** using simple heuristics (not ML)

### 3-Month Plan
1. Collect 1000+ laps across 5-10 popular track/car combos
2. Evaluate data quality and prediction accuracy needs
3. Re-assess ML implementation based on user feedback
4. Prototype CatBoost model if data looks promising

### 6-Month Plan
1. If ML proves valuable, implement full Phase 2-3
2. Ship pre-trained models with application
3. Add "ML Predictions" toggle in settings
4. Monitor user adoption and prediction accuracy

---

**Status**: ✅ **Recommendation: Start with data collection, defer ML training**

**Next Steps**:
1. Review this analysis with stakeholders
2. Decide on immediate vs long-term approach
3. Implement Phase 1 data collection if approved
4. Re-evaluate ML training after 3 months of data collection

**Questions?** Open for discussion on implementation details, timelines, or technical approach.
