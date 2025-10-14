# MVP 3: First Production Widget

**Duration:** 1 week (Actual: ~2 weeks)  
**Goal:** Build production-quality widget system with multiple widgets  
**Status:** ✅ **COMPLETE** (February 2025)  
**Prerequisite:** MVP 1 & 2 must be complete

---

## 🎯 What You Built

**Multiple production widgets** including:

- **DrivingWidget**: Circular speed gauge with gear indicator and configurable side boxes
- **DataWidget**: 2x3 grid with customizable telemetry fields
- **WidgetBase**: Abstract base class providing common functionality
- **Widget Architecture**: Modular system with lifecycle management

---

## ✅ Success Criteria

- ✅ Multiple widgets with different layouts (circular gauge, data grid)
- ✅ Real-time telemetry display with <16ms UI response
- ✅ Updates smoothly without flicker (hardware acceleration)
- ✅ Dynamic field selection and configuration
- ✅ Works in all iRacing session types
- ✅ Color-coded values (tire temps: cold/warming/optimal/hot/extreme)
- ✅ Professional appearance with configurable sizing and transparency

---

## 📁 Key Files to Create

```plaintext
iRacingOverlay.Widgets/
├── Base/
│   ├── IWidget.cs               # Widget interface
│   ├── WidgetBase.cs            # Base class with common functionality
│   └── WidgetConfiguration.cs  # Configuration model
├── Relative/
│   ├── RelativeWidget.xaml      # Widget UI
│   ├── RelativeWidget.xaml.cs   # Code-behind
│   ├── RelativeViewModel.cs     # Business logic
│   ├── Models/
│   │   ├── DriverPosition.cs    # Driver data model
│   │   └── RelativeConfig.cs    # Widget config
│   └── Styles/
│       └── RelativeStyles.xaml  # Widget-specific styles
└── Services/
    └── PositionCalculator.cs    # Gap calculation logic
```

---

## 📋 Implementation Checklist

### Week Planning

#### Days 1-2: Widget Architecture

- [ ] Design `IWidget` interface
- [ ] Implement `WidgetBase` abstract class
- [ ] Create widget lifecycle management
- [ ] Add widget configuration system
- [ ] Design widget positioning system
- [ ] Create unit tests for base classes

#### Days 3-4: Relative Widget UI

- [ ] Design Relative widget layout in XAML
- [ ] Create driver position row template
- [ ] Add color coding system
- [ ] Implement position indicators (↑↓)
- [ ] Add styling and visual polish
- [ ] Test UI responsiveness

#### Days 5-6: Business Logic

- [ ] Implement position tracking algorithm
- [ ] Create gap calculation logic
- [ ] Handle position changes
- [ ] Add class-based filtering
- [ ] Implement lap status detection
- [ ] Test with various field sizes

#### Day 7: Testing & Refinement

- [ ] Integration testing with iRacing
- [ ] Test with 40+ car fields
- [ ] Performance profiling
- [ ] Visual refinement
- [ ] Bug fixes
- [ ] Documentation

---

## ⏱️ Detailed Time Breakdown

| Day | Focus Area | Tasks | Hours |
|-----|-----------|-------|-------|
| **1** | Architecture | Interface design, base classes | 4-5 |
| **2** | Architecture | Configuration, lifecycle, tests | 4-5 |
| **3** | UI Design | XAML layout, templates | 5-6 |
| **4** | UI Polish | Styling, colors, animations | 4-5 |
| **5** | Logic | Position tracking, gap calc | 5-6 |
| **6** | Logic | Class filtering, lap status | 4-5 |
| **7** | Testing | Integration, performance, bugs | 5-6 |

**Total Estimated Hours:** 31-38 hours

---

## 🔧 Technical Details

### IWidget Interface

```csharp
public interface IWidget : INotifyPropertyChanged, IDisposable
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool IsEnabled { get; set; }
    bool IsVisible { get; set; }
    
    WidgetConfiguration Configuration { get; set; }
    
    Task InitializeAsync();
    void Update(TelemetryData telemetry);
    void Show();
    void Hide();
}
```

### WidgetBase Abstract Class

```csharp
public abstract class WidgetBase : ObservableObject, IWidget
{
    protected readonly ITelemetryService TelemetryService;
    protected readonly ILogger Logger;
    
    public string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    
    [ObservableProperty]
    private bool _isEnabled = true;
    
    [ObservableProperty]
    private bool _isVisible = true;
    
    public WidgetConfiguration Configuration { get; set; }
    
    protected WidgetBase(
        ITelemetryService telemetryService,
        ILogger logger)
    {
        Id = Guid.NewGuid().ToString();
        TelemetryService = telemetryService;
        Logger = logger;
    }
    
    public virtual Task InitializeAsync()
    {
        Logger.LogInformation("Initializing widget: {WidgetName}", Name);
        TelemetryService.TelemetryUpdated += OnTelemetryUpdated;
        return Task.CompletedTask;
    }
    
    protected abstract void OnTelemetryUpdated(object? sender, TelemetryData data);
    
    public abstract void Update(TelemetryData telemetry);
    
    public virtual void Show()
    {
        IsVisible = true;
    }
    
    public virtual void Hide()
    {
        IsVisible = false;
    }
    
    public virtual void Dispose()
    {
        TelemetryService.TelemetryUpdated -= OnTelemetryUpdated;
    }
}
```

### DriverPosition Model

```csharp
public class DriverPosition : ObservableObject
{
    [ObservableProperty]
    private int _position;
    
    [ObservableProperty]
    private string _carNumber;
    
    [ObservableProperty]
    private string _driverName;
    
    [ObservableProperty]
    private string _gap; // "2.3s", "+1 Lap", etc.
    
    [ObservableProperty]
    private int _classPosition;
    
    [ObservableProperty]
    private string _carClass;
    
    [ObservableProperty]
    private bool _isPlayer;
    
    [ObservableProperty]
    private int _lapsDown; // 0 = on lead lap, -1 = 1 lap down
    
    [ObservableProperty]
    private PositionChange _change; // Up, Down, Same
    
    public Brush PositionColor => DetermineColor();
    
    private Brush DetermineColor()
    {
        if (IsPlayer)
            return new SolidColorBrush(Colors.Yellow);
        
        if (LapsDown != 0)
            return new SolidColorBrush(Colors.Gray);
        
        // Different colors for different classes
        return new SolidColorBrush(Colors.White);
    }
}

public enum PositionChange
{
    Up,
    Down,
    Same
}
```

### RelativeViewModel

```csharp
public partial class RelativeViewModel : WidgetBase
{
    public override string Name => "Relative";
    public override string Description => "Shows cars ahead and behind";
    
    [ObservableProperty]
    private ObservableCollection<DriverPosition> _drivers = new();
    
    [ObservableProperty]
    private int _carsAhead = 3;
    
    [ObservableProperty]
    private int _carsBehind = 3;
    
    private readonly PositionCalculator _positionCalculator;
    private Dictionary<int, DriverPosition> _previousPositions = new();
    
    public RelativeViewModel(
        ITelemetryService telemetryService,
        ILogger<RelativeViewModel> logger,
        PositionCalculator positionCalculator)
        : base(telemetryService, logger)
    {
        _positionCalculator = positionCalculator;
    }
    
    protected override void OnTelemetryUpdated(object? sender, TelemetryData data)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            UpdateRelativePositions(data);
        });
    }
    
    public override void Update(TelemetryData telemetry)
    {
        UpdateRelativePositions(telemetry);
    }
    
    private void UpdateRelativePositions(TelemetryData telemetry)
    {
        // Get player position
        var playerPosition = telemetry.Position;
        
        // Get all cars in session
        var allCars = telemetry.SessionData.Drivers;
        
        // Calculate positions and gaps
        var relativePositions = _positionCalculator
            .CalculateRelative(allCars, playerPosition, CarsAhead, CarsBehind);
        
        // Detect position changes
        foreach (var pos in relativePositions)
        {
            if (_previousPositions.TryGetValue(pos.CarNumber, out var previous))
            {
                if (pos.Position < previous.Position)
                    pos.Change = PositionChange.Up;
                else if (pos.Position > previous.Position)
                    pos.Change = PositionChange.Down;
                else
                    pos.Change = PositionChange.Same;
            }
        }
        
        // Update collection
        Drivers.Clear();
        foreach (var driver in relativePositions)
        {
            Drivers.Add(driver);
        }
        
        // Store for next update
        _previousPositions = relativePositions.ToDictionary(d => d.CarNumber, d => d);
    }
}
```

### Relative Widget XAML

```xaml
<UserControl x:Class="iRacingOverlay.Widgets.Relative.RelativeWidget"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <Border Background="#DD000000" 
            CornerRadius="5"
            Padding="10">
        <StackPanel>
            <!-- Header -->
            <TextBlock Text="RELATIVE" 
                       Foreground="White"
                       FontSize="14"
                       FontWeight="Bold"
                       HorizontalAlignment="Center"
                       Margin="0,0,0,5"/>
            
            <!-- Driver List -->
            <ItemsControl ItemsSource="{Binding Drivers}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Padding="5,3"
                                Background="{Binding IsPlayer, 
                                    Converter={StaticResource PlayerBackgroundConverter}}">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="30"/>   <!-- Position -->
                                    <ColumnDefinition Width="40"/>   <!-- Car # -->
                                    <ColumnDefinition Width="*"/>    <!-- Name -->
                                    <ColumnDefinition Width="60"/>   <!-- Gap -->
                                    <ColumnDefinition Width="20"/>   <!-- Change -->
                                </Grid.ColumnDefinitions>
                                
                                <!-- Position -->
                                <TextBlock Grid.Column="0"
                                          Text="{Binding Position}"
                                          Foreground="White"
                                          FontSize="12"
                                          VerticalAlignment="Center"/>
                                
                                <!-- Car Number -->
                                <TextBlock Grid.Column="1"
                                          Text="{Binding CarNumber}"
                                          Foreground="{Binding PositionColor}"
                                          FontSize="12"
                                          FontWeight="Bold"
                                          VerticalAlignment="Center"/>
                                
                                <!-- Driver Name -->
                                <TextBlock Grid.Column="2"
                                          Text="{Binding DriverName}"
                                          Foreground="White"
                                          FontSize="12"
                                          TextTrimming="CharacterEllipsis"
                                          VerticalAlignment="Center"
                                          Margin="5,0"/>
                                
                                <!-- Gap -->
                                <TextBlock Grid.Column="3"
                                          Text="{Binding Gap}"
                                          Foreground="#00FF00"
                                          FontSize="12"
                                          FontWeight="Bold"
                                          HorizontalAlignment="Right"
                                          VerticalAlignment="Center"/>
                                
                                <!-- Position Change Indicator -->
                                <TextBlock Grid.Column="4"
                                          Text="{Binding Change, Converter={StaticResource ChangeToSymbolConverter}}"
                                          Foreground="{Binding Change, Converter={StaticResource ChangeToColorConverter}}"
                                          FontSize="14"
                                          FontWeight="Bold"
                                          HorizontalAlignment="Center"
                                          VerticalAlignment="Center"/>
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </StackPanel>
    </Border>
</UserControl>
```

---

## 🎨 Design Specifications

### Color Scheme

| Element | Color | Usage |
|---------|-------|-------|
| Background | #DD000000 | Semi-transparent black |
| Player Row | #FF333300 | Dark yellow highlight |
| Same Class | #FFFFFF | White text |
| Lapped Cars | #808080 | Gray text |
| Positive Gap | #00FF00 | Green (ahead) |
| Negative Gap | #FF0000 | Red (behind) |
| Position Up | #00FF00 | Green arrow |
| Position Down | #FF0000 | Red arrow |

### Layout Dimensions

- **Widget Width:** 350px
- **Widget Height:** Dynamic (based on 7 cars)
- **Row Height:** 25px
- **Padding:** 10px
- **Corner Radius:** 5px
- **Font Size:** 12px (content), 14px (header)

---

## 🐛 Common Issues & Solutions

### Issue: Flickering When Positions Update

**Symptoms:** Widget flickers during position changes

**Solutions:**

- Use ObservableCollection updates instead of Clear/Add
- Implement smart collection updates (only changed items)
- Enable UI virtualization
- Use BeginInit/EndInit for batch updates

### Issue: Incorrect Gap Calculations

**Symptoms:** Time gaps don't match reality

**Solutions:**

- Use LapDistPct for accurate positioning
- Account for track length in calculations
- Handle lap transition edge cases
- Test with various track lengths

### Issue: Missing Drivers

**Symptoms:** Not all nearby drivers shown

**Solutions:**

- Verify session data parsing
- Check driver filtering logic
- Handle pitstop scenarios
- Account for DNF/disconnected drivers

---

## 🧪 Testing Scenarios

### Test 1: Basic Functionality

1. Start 20+ car race
2. Verify 3 ahead + player + 3 behind shown
3. Check all driver info displays correctly
4. Verify gaps are reasonable

### Test 2: Position Changes

1. Overtake a car
2. Verify position updates immediately
3. Check up/down indicators appear
4. Verify gap calculations adjust

### Test 3: Large Fields

1. Join 40+ car session
2. Verify performance remains good
3. Check correct relative cars shown
4. Test at different positions (front, mid, back)

### Test 4: Edge Cases

1. Test when in 1st place (no cars ahead)
2. Test when in last place (no cars behind)
3. Test with lapped cars
4. Test in practice (sparse field)

---

## 📊 Performance Targets

| Metric | Target | Acceptable | Critical |
|--------|--------|------------|----------|
| **Update Rate** | 60Hz | 30Hz+ | <20Hz |
| **CPU Usage** | <5% | <8% | >12% |
| **Memory Usage** | <100MB | <120MB | >150MB |
| **UI Latency** | <16ms | <33ms | >50ms |
| **40-Car Field** | No impact | <5% slower | >10% slower |

---

## ✨ Completion Checklist

Before moving to MVP 4, ensure:

- [ ] All success criteria met
- [ ] Widget displays correctly with 7 cars
- [ ] Gap calculations accurate
- [ ] Position changes detected
- [ ] Color coding working
- [ ] Tested with 40+ car field
- [ ] Performance targets achieved
- [ ] Code is well-documented
- [ ] Unit tests written
- [ ] Integration tests passing
- [ ] Code committed to Git

---

## 🚀 Next Steps

Once MVP 3 is complete:

1. Review this document and check all boxes
2. Update status to 🟢 Complete
3. Capture demo video/screenshots
4. Write developer documentation
5. Commit all code changes
6. Move to **MVP 4: Multi-Widget System**

---

## 📚 Resources

- [Racelabs Relative Reference](https://racelabs.app/) - Visual reference
- [iRacing Session Data](https://github.com/kutu/pyirsdk) - Data structure examples
- [WPF ItemsControl](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/itemscontrol)
- [ObservableCollection Best Practices](https://learn.microsoft.com/en-us/dotnet/api/system.collections.objectmodel.observablecollection-1)

---

**Created:** October 12, 2025  
**Last Updated:** October 12, 2025  
**Next Review:** After completion  
**Related:** [MVP 2](./MVP2_Simple_Overlay.md) | [MVP 4](./MVP4_Multi_Widget.md) | [Overview](../../MANAGEABLE_PHASES.md)
