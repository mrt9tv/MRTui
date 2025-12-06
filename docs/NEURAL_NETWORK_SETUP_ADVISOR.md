# Neural Network Setup Advisor - Phase 3.7
## AI-Powered Setup Recommendations

**Status**: 📋 Design Phase  
**Timeline**: 2-3 weeks (Dec 9 - Dec 27, 2025)  
**Goal**: Analyze current setup for 5-10 laps and proactively suggest improvements

---

## Vision

**Current System** (Physics Heuristics):
- Compares Setup A vs Setup B after both tested
- Simple linear relationships ("+1 wing = -0.015s")
- Requires manual setup changes

**New System** (Neural Network):
- Analyzes current setup in real-time
- Detects handling issues automatically (oversteer, understeer, lockups)
- Recommends 3-5 specific changes with confidence scores
- **Example**: "Oversteer detected in Turn 7 (severity 8/10) → Try +2 clicks rear ARB → Predicted: -0.12s"

---

## Architecture Overview

### Data Flow

```
Live Telemetry (60Hz)
    ↓
Corner Segmentation (identify Turn 1, Turn 2, etc.)
    ↓
Handling Detection (oversteer, understeer, lockups)
    ↓
Feature Engineering (50+ features)
    ↓
Neural Network (ONNX model)
    ↓
Ranked Recommendations (Top 3-5 changes)
    ↓
UI Display + A/B Testing
```

### Neural Network Architecture

**Input Layer**: 50+ features
- Setup parameters (15): Wing, ARB, ride height, dampers, tire pressure, camber, toe
- Driving behavior (10): Oversteer/understeer severity, lockups, steering corrections, throttle smoothness
- Tire data (12): Temps (LF/RF/LR/RR × 3 zones), wear, pressure, degradation rate
- Track conditions (5): Air temp, track temp, time of day, rubber buildup, track wetness
- Lap performance (8): Sector times, corner speeds, throttle/brake scores, consistency

**Hidden Layers**:
- Layer 1: 128 neurons (ReLU activation) - Feature extraction
- Dropout: 20% (prevent overfitting)
- Layer 2: 64 neurons (ReLU) - Pattern recognition
- Dropout: 20%
- Layer 3: 32 neurons (ReLU) - Decision making

**Output Layer**: 10 continuous values
- Front wing adjustment: -3.0 to +3.0 clicks
- Rear wing adjustment: -3.0 to +3.0 clicks
- Front ARB adjustment: -3.0 to +3.0 clicks
- Rear ARB adjustment: -3.0 to +3.0 clicks
- Front tire pressure: -2.0 to +2.0 kPa
- Rear tire pressure: -2.0 to +2.0 kPa
- Front damper compression: -2.0 to +2.0 clicks
- Rear damper compression: -2.0 to +2.0 clicks
- Predicted lap time delta: seconds (negative = faster)
- Confidence score: 0.0 to 1.0 (70%+ = show recommendation)

**Loss Function**: Custom hybrid loss
```
Loss = α * MSE(lap_time_delta) + β * CrossEntropy(confidence)
```
- α = 0.7 (prioritize lap time accuracy)
- β = 0.3 (ensure confident predictions)

---

## Feature Engineering

### 1. Automatic Handling Detection

**No user input required!** All detected from telemetry.

#### Oversteer Detection

**Algorithm**:
```csharp
public float DetectOversteer(List<TelemetryData> cornerData)
{
    // Oversteer signature: rapid steering corrections + low lateral G
    var steeringRate = CalculateSteeringRate(cornerData);
    var lateralG = cornerData.Average(d => Math.Abs(d.LatAccel));
    var yawRate = cornerData.Average(d => Math.Abs(d.YawRate));
    
    // Sliding rear = high yaw rate + low lateral grip
    if (yawRate > 0.15f && lateralG < 1.2f && steeringRate > 0.3f)
        return 8.0f; // Severe oversteer
    
    if (yawRate > 0.10f && lateralG < 1.5f)
        return 5.0f; // Moderate oversteer
    
    return 0.0f; // Neutral
}
```

**Key Indicators**:
- High steering angle change rate (>0.3 rad/s)
- Low lateral acceleration (<1.2g while turning)
- High yaw rate (>0.15 rad/s)
- Rear slip angle > Front slip angle

#### Understeer Detection

**Algorithm**:
```csharp
public float DetectUndersteer(List<TelemetryData> cornerData)
{
    // Understeer signature: high throttle + low speed gain (scrubbing)
    var throttleAvg = cornerData.Average(d => d.Throttle);
    var speedGain = cornerData.Last().Speed - cornerData.First().Speed;
    var steeringAngle = cornerData.Average(d => Math.Abs(d.SteeringWheelAngle));
    var lateralG = cornerData.Average(d => Math.Abs(d.LatAccel));
    
    // Plowing front = high steering + high throttle + low speed gain
    if (steeringAngle > 90f && throttleAvg > 0.7f && speedGain < 5.0f)
        return 8.0f; // Severe understeer
    
    if (steeringAngle > 60f && throttleAvg > 0.5f && speedGain < 10.0f)
        return 5.0f; // Moderate understeer
    
    return 0.0f; // Neutral
}
```

**Key Indicators**:
- High steering angle (>90°)
- High throttle (>70%)
- Low speed gain through corner (<5 km/h)
- Front slip angle > Rear slip angle

#### Brake Lockup Detection

**Algorithm**:
```csharp
public int DetectLockups(List<TelemetryData> brakingData)
{
    int lockupCount = 0;
    
    foreach (var data in brakingData)
    {
        if (data.Brake > 0.5f)
        {
            // Lockup: wheel speed < 80% of vehicle speed
            var avgWheelSpeed = (data.LFspeed + data.RFspeed) / 2f;
            var vehicleSpeed = data.Speed;
            
            if (avgWheelSpeed < vehicleSpeed * 0.8f)
            {
                lockupCount++;
                
                // Identify which wheel locked
                if (data.LFspeed < vehicleSpeed * 0.8f)
                    Console.WriteLine($"LF lockup @ {data.Speed:F1} km/h");
                if (data.RFspeed < vehicleSpeed * 0.8f)
                    Console.WriteLine($"RF lockup @ {data.Speed:F1} km/h");
            }
        }
    }
    
    return lockupCount;
}
```

**Key Indicators**:
- Wheel speed < 80% of vehicle speed
- Brake pressure > 50%
- Long accel spike (tire sliding on surface)

### 2. Corner Segmentation

**Goal**: Identify corners automatically (no manual track mapping needed)

**Algorithm**:
```csharp
public List<CornerSegment> SegmentCorners(List<TelemetryData> lapData)
{
    var corners = new List<CornerSegment>();
    var inCorner = false;
    var cornerStart = 0;
    
    for (int i = 0; i < lapData.Count; i++)
    {
        var data = lapData[i];
        var isCornerFrame = Math.Abs(data.LatAccel) > 0.5f || Math.Abs(data.SteeringWheelAngle) > 30f;
        
        if (!inCorner && isCornerFrame)
        {
            // Corner entry
            inCorner = true;
            cornerStart = i;
        }
        else if (inCorner && !isCornerFrame)
        {
            // Corner exit
            inCorner = false;
            corners.Add(new CornerSegment
            {
                StartIndex = cornerStart,
                EndIndex = i,
                Data = lapData.GetRange(cornerStart, i - cornerStart)
            });
        }
    }
    
    return corners;
}
```

**Output**: List of corner segments with:
- Entry speed
- Apex speed
- Exit speed
- Lateral G profile
- Steering angle profile
- Handling issues detected

### 3. Tire Degradation Tracking

**Algorithm**:
```csharp
public TireDegradationData AnalyzeTireDeg(List<FuelLapHistory> laps)
{
    var lfTemps = laps.Select(l => l.AvgTireTemps[0]).ToList();
    var rfTemps = laps.Select(l => l.AvgTireTemps[1]).ToList();
    
    // Calculate temperature increase per lap
    var lfDegRate = (lfTemps.Last() - lfTemps.First()) / laps.Count;
    var rfDegRate = (rfTemps.Last() - rfTemps.First()) / laps.Count;
    
    // Imbalance = LF vs RF temperature delta
    var avgLF = lfTemps.Average();
    var avgRF = rfTemps.Average();
    var imbalance = Math.Abs(avgLF - avgRF);
    
    return new TireDegradationData
    {
        FrontDegRate = (lfDegRate + rfDegRate) / 2f,
        RearDegRate = CalculateRearDegRate(laps),
        Imbalance = imbalance,
        CriticalLap = PredictCriticalLap(lfDegRate, avgLF)
    };
}
```

---

## Neural Network Training

### Training Dataset

**Sources**:
1. **iRacing Community Data** (public telemetry):
   - 10,000+ laps from top-split drivers
   - Various cars (GT3, LMP2, Formula)
   - Various tracks (oval, road, street)

2. **User Historical Data**:
   - Your own laps from Setup Engineering Mode
   - A/B testing results (setup change → lap time delta)

3. **Synthetic Data** (data augmentation):
   - Physics simulator generates edge cases
   - Temperature variations
   - Track condition variations

**Dataset Size**: 50,000+ laps (after augmentation)

**Split**:
- Training: 70% (35,000 laps)
- Validation: 15% (7,500 laps)
- Testing: 15% (7,500 laps)

### Training Pipeline (PyTorch)

```python
import torch
import torch.nn as nn
import torch.optim as optim

class SetupAdvisorNN(nn.Module):
    def __init__(self):
        super().__init__()
        
        # Input: 50 features
        self.fc1 = nn.Linear(50, 128)
        self.dropout1 = nn.Dropout(0.2)
        self.fc2 = nn.Linear(128, 64)
        self.dropout2 = nn.Dropout(0.2)
        self.fc3 = nn.Linear(64, 32)
        
        # Output: 10 values (8 adjustments + lap delta + confidence)
        self.output = nn.Linear(32, 10)
        
        self.relu = nn.ReLU()
        self.sigmoid = nn.Sigmoid()
    
    def forward(self, x):
        x = self.relu(self.fc1(x))
        x = self.dropout1(x)
        x = self.relu(self.fc2(x))
        x = self.dropout2(x)
        x = self.relu(self.fc3(x))
        
        out = self.output(x)
        
        # Adjustments: tanh activation (range -3 to +3)
        adjustments = 3.0 * torch.tanh(out[:, :8])
        
        # Lap delta: linear (can be positive or negative)
        lap_delta = out[:, 8:9]
        
        # Confidence: sigmoid (range 0 to 1)
        confidence = self.sigmoid(out[:, 9:10])
        
        return torch.cat([adjustments, lap_delta, confidence], dim=1)

# Training loop
model = SetupAdvisorNN()
optimizer = optim.Adam(model.parameters(), lr=0.001)
criterion = CustomLoss(alpha=0.7, beta=0.3)

for epoch in range(100):
    for batch in train_loader:
        features, targets = batch
        
        # Forward pass
        predictions = model(features)
        loss = criterion(predictions, targets)
        
        # Backward pass
        optimizer.zero_grad()
        loss.backward()
        optimizer.step()
    
    # Validation
    val_loss = validate(model, val_loader)
    print(f"Epoch {epoch}: Loss={loss:.4f}, Val Loss={val_loss:.4f}")

# Export to ONNX
torch.onnx.export(model, dummy_input, "setup_advisor.onnx")
```

### Custom Loss Function

```python
class CustomLoss(nn.Module):
    def __init__(self, alpha=0.7, beta=0.3):
        super().__init__()
        self.alpha = alpha
        self.beta = beta
    
    def forward(self, predictions, targets):
        # Predictions: [adjustments (8), lap_delta (1), confidence (1)]
        pred_adjustments = predictions[:, :8]
        pred_delta = predictions[:, 8]
        pred_confidence = predictions[:, 9]
        
        target_adjustments = targets[:, :8]
        target_delta = targets[:, 8]
        target_confidence = targets[:, 9]
        
        # Lap time delta loss (MSE)
        delta_loss = F.mse_loss(pred_delta, target_delta)
        
        # Confidence loss (binary cross-entropy)
        confidence_loss = F.binary_cross_entropy(pred_confidence, target_confidence)
        
        # Combined loss
        total_loss = self.alpha * delta_loss + self.beta * confidence_loss
        
        return total_loss
```

---

## C# Integration (ONNX Runtime)

### MLModelService Enhancement

```csharp
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

public class MLModelService
{
    private InferenceSession? _setupAdvisorSession;
    
    public async Task LoadNeuralNetworkAsync()
    {
        var modelPath = Path.Combine(_modelsDirectory, "setup_advisor.onnx");
        
        if (File.Exists(modelPath))
        {
            _setupAdvisorSession = new InferenceSession(modelPath);
            Console.WriteLine($"[MLModelService] ✅ Loaded NN model: {modelPath}");
        }
    }
    
    public SetupRecommendation AnalyzeCurrentSetup(
        SetupParameterFeatures currentSetup,
        DrivingBehaviorFeatures behavior,
        TelemetryData telemetry,
        List<FuelLapHistory> recentLaps)
    {
        if (_setupAdvisorSession == null)
            return FallbackToPhysicsHeuristics(currentSetup, behavior);
        
        // 1. Extract 50+ features
        var features = ExtractNNFeatures(currentSetup, behavior, telemetry, recentLaps);
        
        // 2. Create ONNX tensor
        var inputTensor = new DenseTensor<float>(features, new[] { 1, 50 });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor)
        };
        
        // 3. Run inference
        using var results = _setupAdvisorSession.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();
        
        // 4. Parse output
        var recommendation = new SetupRecommendation
        {
            FrontWingAdjustment = output[0],
            RearWingAdjustment = output[1],
            FrontARBAdjustment = output[2],
            RearARBAdjustment = output[3],
            FrontTirePressureAdjustment = output[4],
            RearTirePressureAdjustment = output[5],
            FrontDamperAdjustment = output[6],
            RearDamperAdjustment = output[7],
            PredictedLapTimeDelta = output[8],
            Confidence = output[9]
        };
        
        // 5. Filter low-confidence predictions
        if (recommendation.Confidence < 0.70f)
            return null; // Not confident enough
        
        // 6. Generate human-readable recommendations
        recommendation.Recommendations = GenerateRecommendationText(recommendation, behavior);
        
        return recommendation;
    }
    
    private float[] ExtractNNFeatures(
        SetupParameterFeatures setup,
        DrivingBehaviorFeatures behavior,
        TelemetryData telemetry,
        List<FuelLapHistory> recentLaps)
    {
        var features = new float[50];
        int idx = 0;
        
        // Setup parameters (15)
        features[idx++] = setup.FrontWing ?? 0f;
        features[idx++] = setup.RearWing ?? 0f;
        features[idx++] = setup.FrontARB ?? 0f;
        features[idx++] = setup.RearARB ?? 0f;
        features[idx++] = setup.FrontRideHeight ?? 0f;
        features[idx++] = setup.RearRideHeight ?? 0f;
        features[idx++] = setup.LFTirePressure ?? 0f;
        features[idx++] = setup.RFTirePressure ?? 0f;
        features[idx++] = setup.LRTirePressure ?? 0f;
        features[idx++] = setup.RRTirePressure ?? 0f;
        // ... add remaining 5 setup params
        
        // Driving behavior (10)
        features[idx++] = behavior.OversteerSeverity;
        features[idx++] = behavior.UndersteerSeverity;
        features[idx++] = behavior.BrakeStability;
        features[idx++] = behavior.CornerEntryInstability;
        features[idx++] = behavior.CornerExitTraction;
        features[idx++] = behavior.ThrottleSmoothnessScore;
        features[idx++] = behavior.BrakeModulationScore;
        features[idx++] = behavior.SteeringConsistency;
        features[idx++] = behavior.FrontTireDegRate;
        features[idx++] = behavior.RearTireDegRate;
        
        // Tire data (12)
        features[idx++] = telemetry.LFtempCM;
        features[idx++] = telemetry.RFtempCM;
        features[idx++] = telemetry.LRtempCM;
        features[idx++] = telemetry.RRtempCM;
        // ... add remaining 8 tire features
        
        // Track conditions (5)
        features[idx++] = telemetry.AirTemp;
        features[idx++] = telemetry.TrackTemp;
        // ... add remaining 3 track features
        
        // Lap performance (8)
        var avgLapTime = recentLaps.Average(l => l.LapTime);
        features[idx++] = avgLapTime;
        // ... add remaining 7 performance features
        
        return features;
    }
    
    private List<string> GenerateRecommendationText(
        SetupRecommendation reco,
        DrivingBehaviorFeatures behavior)
    {
        var recommendations = new List<string>();
        
        // Prioritize by adjustment magnitude
        var adjustments = new[]
        {
            ("Front Wing", reco.FrontWingAdjustment, "clicks"),
            ("Rear Wing", reco.RearWingAdjustment, "clicks"),
            ("Front ARB", reco.FrontARBAdjustment, "clicks"),
            ("Rear ARB", reco.RearARBAdjustment, "clicks"),
            ("Front Tire Pressure", reco.FrontTirePressureAdjustment, "kPa"),
            ("Rear Tire Pressure", reco.RearTirePressureAdjustment, "kPa")
        };
        
        foreach (var (param, value, unit) in adjustments.OrderByDescending(a => Math.Abs(a.Item2)))
        {
            if (Math.Abs(value) > 0.5f)
            {
                var direction = value > 0 ? "+" : "";
                var reason = GetReasonForAdjustment(param, value, behavior);
                recommendations.Add($"{param}: {direction}{value:F1} {unit} → {reason}");
            }
        }
        
        // Add predicted improvement
        if (reco.PredictedLapTimeDelta < 0)
        {
            recommendations.Add($"⏱️ Predicted: {-reco.PredictedLapTimeDelta:F3}s faster");
        }
        
        return recommendations;
    }
    
    private string GetReasonForAdjustment(string param, float value, DrivingBehaviorFeatures behavior)
    {
        if (param == "Rear ARB" && value > 0 && behavior.OversteerSeverity > 6f)
            return "Reduce oversteer (detected severity 8/10)";
        
        if (param == "Front Wing" && value < 0 && behavior.UndersteerSeverity > 6f)
            return "Reduce understeer (front pushing)";
        
        if (param == "Front Tire Pressure" && value < 0 && behavior.FrontTireDegRate > 1.5f)
            return "Reduce front tire overheating";
        
        return "Optimize balance";
    }
}
```

---

## UI Integration

### Setup Engineering Window Enhancement

```xml
<!-- New tab: AI Recommendations -->
<TabItem Header="🤖 AI Recommendations">
    <ScrollViewer>
        <StackPanel Margin="20">
            <!-- Status -->
            <TextBlock Text="Neural Network Setup Advisor" FontSize="18" FontWeight="Bold" Margin="0,0,0,10"/>
            <TextBlock Name="AIStatusText" Text="Analyzing current setup..." FontSize="14" Foreground="Gray" Margin="0,0,0,20"/>
            
            <!-- Lap Counter -->
            <Border Background="#2D2D30" BorderBrush="#007ACC" BorderThickness="2" Padding="15" Margin="0,0,0,20">
                <StackPanel>
                    <TextBlock Text="Analysis Progress" FontSize="14" FontWeight="SemiBold" Margin="0,0,0,10"/>
                    <ProgressBar Name="AnalysisProgressBar" Height="20" Minimum="0" Maximum="10" Value="0"/>
                    <TextBlock Name="LapCounterText" Text="0/10 laps collected" FontSize="12" Foreground="Gray" Margin="0,5,0,0"/>
                </StackPanel>
            </Border>
            
            <!-- Detected Issues -->
            <TextBlock Text="Detected Issues" FontSize="16" FontWeight="SemiBold" Margin="0,0,0,10"/>
            <ItemsControl Name="DetectedIssuesList">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Background="#3F3F46" Padding="10" Margin="0,0,0,5">
                            <StackPanel>
                                <TextBlock Text="{Binding IssueDescription}" FontSize="14" FontWeight="SemiBold"/>
                                <TextBlock Text="{Binding SeverityText}" FontSize="12" Foreground="{Binding SeverityColor}" Margin="0,5,0,0"/>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
            
            <!-- AI Recommendations -->
            <TextBlock Text="AI Recommendations" FontSize="16" FontWeight="SemiBold" Margin="0,20,0,10"/>
            <ItemsControl Name="AIRecommendationsList">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Background="#2D2D30" BorderBrush="#00D4FF" BorderThickness="2" Padding="15" Margin="0,0,0,10">
                            <StackPanel>
                                <TextBlock Text="{Binding Title}" FontSize="14" FontWeight="SemiBold"/>
                                <TextBlock Text="{Binding Reason}" FontSize="12" Foreground="Gray" Margin="0,5,0,10" TextWrapping="Wrap"/>
                                <StackPanel Orientation="Horizontal" Margin="0,5,0,0">
                                    <TextBlock Text="Predicted:" FontSize="12" Foreground="Gray" Margin="0,0,5,0"/>
                                    <TextBlock Text="{Binding PredictedImprovement}" FontSize="12" Foreground="#00D4FF" FontWeight="SemiBold"/>
                                </StackPanel>
                                <StackPanel Orientation="Horizontal" Margin="0,5,0,0">
                                    <TextBlock Text="Confidence:" FontSize="12" Foreground="Gray" Margin="0,0,5,0"/>
                                    <TextBlock Text="{Binding ConfidenceText}" FontSize="12" FontWeight="SemiBold"/>
                                </StackPanel>
                                <Button Name="TryRecommendationButton" Content="Try This Change" Margin="0,10,0,0" Click="TryRecommendationButton_Click"/>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
            
            <!-- Manual Refresh -->
            <Button Name="RefreshAIButton" Content="🔄 Refresh AI Analysis" Margin="0,20,0,0" Click="RefreshAIButton_Click"/>
        </StackPanel>
    </ScrollViewer>
</TabItem>
```

---

## A/B Testing Framework

**Goal**: Learn from user feedback to improve model

```csharp
public class ABTestingService
{
    public async Task RecordTestAsync(
        string setupId,
        SetupRecommendation recommendation,
        bool userAccepted)
    {
        if (!userAccepted)
            return;
        
        // Wait for user to complete 5 laps with new setup
        var newLaps = await WaitForNewLaps(setupId, lapCount: 5);
        
        // Compare actual vs predicted lap time delta
        var actualDelta = CalculateActualDelta(newLaps);
        var predictedDelta = recommendation.PredictedLapTimeDelta;
        var accuracy = 1.0f - Math.Abs(actualDelta - predictedDelta) / Math.Abs(predictedDelta);
        
        // Store result for model retraining
        await _database.RecordABTestAsync(new ABTestResult
        {
            SetupId = setupId,
            Recommendation = recommendation,
            PredictedDelta = predictedDelta,
            ActualDelta = actualDelta,
            Accuracy = accuracy,
            Timestamp = DateTime.UtcNow
        });
        
        // Feedback to user
        Console.WriteLine($"[ABTesting] Predicted: {predictedDelta:F3}s, Actual: {actualDelta:F3}s, Accuracy: {accuracy:P0}");
    }
}
```

---

## Implementation Timeline

### Week 1: Handling Detection + Feature Engineering (Dec 9-13)

**Day 1-2**: Implement detection algorithms
- ✅ `HandlingDetector.DetectOversteer()`
- ✅ `HandlingDetector.DetectUndersteer()`
- ✅ `HandlingDetector.DetectLockups()`
- ✅ `CornerSegmenter.SegmentCorners()`

**Day 3**: Feature extraction
- ✅ `DrivingBehaviorFeatures` model
- ✅ `MLModelService.ExtractNNFeatures()`

**Day 4**: Unit tests
- Test oversteer detection with mock telemetry
- Test corner segmentation accuracy
- Test feature extraction completeness

**Day 5**: Integration with UI
- Live issue detection display
- Progress bar for lap collection

### Week 2: Model Training + ONNX Integration (Dec 16-20)

**Day 1-2**: Collect training data
- Download public iRacing telemetry datasets
- Extract 50,000+ laps
- Label data (setup changes → lap time deltas)

**Day 3-4**: Train neural network
- Implement PyTorch model
- Train for 100 epochs
- Validate on test set
- Export to ONNX

**Day 5**: C# ONNX integration
- Load ONNX model in MLModelService
- Run inference on test data
- Verify predictions match PyTorch

### Week 3: Polish + A/B Testing (Dec 23-27)

**Day 1-2**: UI polish
- Recommendation display
- Confidence thresholds
- User feedback buttons

**Day 3**: A/B testing framework
- Record user acceptance
- Track actual vs predicted results
- Store for retraining

**Day 4**: Documentation
- User guide for AI recommendations
- Model architecture documentation

**Day 5**: End-to-end testing
- Live iRacing session with 10+ laps
- Verify recommendations generate correctly
- Test A/B testing workflow

---

## Success Metrics

### Phase 3.7 Success Criteria

1. **Accuracy**: 
   - ✅ Predicted lap time delta within ±0.10s of actual (80% of recommendations)
   - ✅ Oversteer/understeer detection accuracy >85%

2. **User Adoption**:
   - ✅ 60% of users try at least 1 AI recommendation
   - ✅ 40% of users try 3+ recommendations

3. **Performance**:
   - ✅ Inference time <100ms (per lap analysis)
   - ✅ Model size <50MB (ONNX)

4. **Improvement**:
   - ✅ AI recommendations reduce setup tuning time by 40%
   - ✅ 70% of accepted recommendations improve lap times

---

## Future Enhancements (Phase 4+)

1. **Multi-Car Learning**: Transfer learning across GT3 cars
2. **Driver Style Profiles**: "Aggressive" vs "Smooth" driving styles
3. **Track-Specific Models**: Dedicated NN for Spa, Monza, etc.
4. **Real-Time Recommendations**: Suggest changes mid-session
5. **Telemetry Comparison**: Compare your driving vs aliens
6. **Voice Integration**: "Copilot, why am I losing time in Turn 7?"

---

**End of Design Document**  
**Next**: Begin implementation (Week 1 tasks)
