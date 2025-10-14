# MVP 2: Simple Overlay Window

**Duration:** 3-5 days (Actual: ~1 week)  
**Goal:** Display telemetry data in a transparent overlay  
**Status:** ✅ **COMPLETE** (January 2025)  
**Prerequisite:** MVP 1 must be complete

---

## 🎯 What You Built

A WPF overlay that:

- Shows as transparent window on top of iRacing (always topmost)
- Displays multiple telemetry values via widgets
- Updates in real-time (60Hz telemetry rate)
- Can be moved and resized dynamically
- Hardware-accelerated rendering for smooth performance

---

## ✅ Success Criteria

- ✅ Transparent overlay visible over iRacing (AllowsTransparency, Topmost)
- ✅ Shows 10+ telemetry values across multiple widgets
- ✅ Updates smoothly at 60Hz (via DispatcherTimer)
- ✅ Window stays on top of game (Topmost = true)
- ✅ Can be repositioned by dragging (DragMove)
- ✅ No significant frame rate impact (<5% CPU, hardware accelerated)

---

## 📁 Key Files to Create

```plaintext
iRacingOverlay.Overlay/
├── App.xaml
├── App.xaml.cs
├── OverlayWindow.xaml           # Main overlay UI
├── OverlayWindow.xaml.cs        # Code-behind
├── ViewModels/
│   └── OverlayViewModel.cs      # MVVM pattern
├── Models/
│   └── DisplayData.cs           # Display data model
└── Styles/
    └── OverlayStyles.xaml       # Styling resources
```

---

## 📋 Implementation Checklist

### Setup Phase

- [ ] Create new WPF (.NET 8) project
- [ ] Add project reference to MVP 1 console app
- [ ] Install required NuGet packages (CommunityToolkit.Mvvm)
- [ ] Set up basic project structure

### UI Development

- [ ] Design transparent window in XAML
- [ ] Create basic layout for 5 metrics
- [ ] Add styling (fonts, colors, spacing)
- [ ] Implement always-on-top behavior
- [ ] Add window drag functionality

### ViewModel & Data Binding

- [ ] Create `OverlayViewModel` class
- [ ] Implement `INotifyPropertyChanged`
- [ ] Add properties for 5 telemetry values
- [ ] Set up data binding in XAML
- [ ] Test property change notifications

### Integration

- [ ] Connect TelemetryService from MVP 1
- [ ] Wire telemetry events to ViewModel
- [ ] Implement thread-safe UI updates (Dispatcher)
- [ ] Add connection status indicator
- [ ] Test real-time updates with iRacing

### Polish & Optimization

- [ ] Enable hardware acceleration
- [ ] Optimize rendering performance
- [ ] Add smooth value transitions (optional)
- [ ] Test with different iRacing graphics settings
- [ ] Verify no game performance impact

---

## ⏱️ Time Breakdown

| Day | Tasks | Hours |
|-----|-------|-------|
| **Day 1** | WPF project setup + window design | 3-4 |
| **Day 2** | Transparency + click-through | 3-4 |
| **Day 3** | Data binding + ViewModel | 4-5 |
| **Day 4** | Connect telemetry to UI | 3-4 |
| **Day 5** | Styling + performance testing | 3-4 |

**Total Estimated Hours:** 16-21 hours

---

## 🔧 Technical Details

### Required NuGet Packages

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
```

### Transparent Window XAML

```xaml
<Window x:Class="iRacingOverlay.Overlay.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="iRacing Overlay"
        Width="300" Height="200"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        MouseLeftButtonDown="Window_MouseLeftButtonDown">
    
    <Border Background="#CC000000" 
            CornerRadius="10"
            Padding="15">
        <StackPanel>
            <!-- Speed Display -->
            <StackPanel Orientation="Horizontal" Margin="0,5">
                <TextBlock Text="Speed:" 
                           Foreground="White" 
                           Width="80"
                           FontSize="16"/>
                <TextBlock Text="{Binding Speed, StringFormat='{}{0:F1} km/h'}" 
                           Foreground="#00FF00" 
                           FontSize="16"
                           FontWeight="Bold"/>
            </StackPanel>
            
            <!-- RPM Display -->
            <StackPanel Orientation="Horizontal" Margin="0,5">
                <TextBlock Text="RPM:" 
                           Foreground="White" 
                           Width="80"
                           FontSize="16"/>
                <TextBlock Text="{Binding RPM, StringFormat='{}{0:F0}'}" 
                           Foreground="#FF6600" 
                           FontSize="16"
                           FontWeight="Bold"/>
            </StackPanel>
            
            <!-- Gear Display -->
            <StackPanel Orientation="Horizontal" Margin="0,5">
                <TextBlock Text="Gear:" 
                           Foreground="White" 
                           Width="80"
                           FontSize="16"/>
                <TextBlock Text="{Binding Gear}" 
                           Foreground="#FFFF00" 
                           FontSize="16"
                           FontWeight="Bold"/>
            </StackPanel>
            
            <!-- Lap Display -->
            <StackPanel Orientation="Horizontal" Margin="0,5">
                <TextBlock Text="Lap:" 
                           Foreground="White" 
                           Width="80"
                           FontSize="16"/>
                <TextBlock Text="{Binding CurrentLap}" 
                           Foreground="#00BFFF" 
                           FontSize="16"
                           FontWeight="Bold"/>
            </StackPanel>
            
            <!-- Position Display -->
            <StackPanel Orientation="Horizontal" Margin="0,5">
                <TextBlock Text="Position:" 
                           Foreground="White" 
                           Width="80"
                           FontSize="16"/>
                <TextBlock Text="{Binding Position}" 
                           Foreground="#FF00FF" 
                           FontSize="16"
                           FontWeight="Bold"/>
            </StackPanel>
        </StackPanel>
    </Border>
</Window>
```

### ViewModel Implementation

```csharp
public partial class OverlayViewModel : ObservableObject
{
    private readonly ITelemetryService _telemetryService;
    
    [ObservableProperty]
    private double _speed;
    
    [ObservableProperty]
    private int _rpm;
    
    [ObservableProperty]
    private int _gear;
    
    [ObservableProperty]
    private int _currentLap;
    
    [ObservableProperty]
    private int _position;
    
    public OverlayViewModel(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
        _telemetryService.TelemetryUpdated += OnTelemetryUpdated;
    }
    
    private void OnTelemetryUpdated(object? sender, TelemetryData data)
    {
        // Update on UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            Speed = data.Speed * 3.6; // Convert m/s to km/h
            RPM = data.RPM;
            Gear = data.Gear;
            CurrentLap = data.Lap;
            Position = data.Position;
        });
    }
}
```

### Code-Behind for Dragging

```csharp
public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
    }
    
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            this.DragMove();
        }
    }
}
```

---

## 🎨 Design Considerations

### Color Scheme

- **Background:** Semi-transparent dark (#CC000000)
- **Labels:** White (#FFFFFF)
- **Speed:** Green (#00FF00)
- **RPM:** Orange (#FF6600)
- **Gear:** Yellow (#FFFF00)
- **Lap:** Blue (#00BFFF)
- **Position:** Magenta (#FF00FF)

### Typography

- **Font:** Segoe UI (default Windows font)
- **Label Size:** 16px
- **Value Size:** 16px Bold
- **Spacing:** 5px between rows

### Layout

- **Compact:** Fits in ~300x200 window
- **Readable:** Clear contrast between text and background
- **Organized:** Labels aligned left, values follow

---

## 🐛 Common Issues & Solutions

### Issue: Window Not Transparent

**Symptoms:** Window shows white background

**Solutions:**

- Ensure `AllowsTransparency="True"` is set
- Set `WindowStyle="None"`
- Background must be `Transparent`
- Check hardware acceleration is enabled

### Issue: Can't Click Through Window

**Symptoms:** Window blocks clicks to iRacing

**Solutions:**

- This is expected for MVP 2
- Will be addressed in later MVPs
- For now, position window where it won't interfere

### Issue: UI Not Updating

**Symptoms:** Values don't change in overlay

**Solutions:**

- Verify telemetry service is connected
- Check event handler is registered
- Ensure Dispatcher.Invoke is used for UI thread
- Add debug logging to track data flow

### Issue: Poor Performance / Stuttering

**Symptoms:** Overlay causes FPS drops in iRacing

**Solutions:**

- Enable hardware acceleration in XAML
- Reduce update frequency if needed
- Check CPU usage with Task Manager
- Simplify UI rendering (fewer effects)

---

## 🧪 Testing Scenarios

### Test 1: Basic Display

1. Start iRacing and enter session
2. Launch overlay application
3. Verify all 5 metrics display correctly
4. Drive around and verify values update

### Test 2: Window Positioning

1. Drag window to different screen positions
2. Verify window moves smoothly
3. Test in corners and edges
4. Verify window stays on top of iRacing

### Test 3: Transparency

1. Check window background is semi-transparent
2. Verify can see iRacing through window
3. Check text is clearly readable
4. Test with different iRacing graphics settings

### Test 4: Performance

1. Run iRacing with overlay active
2. Monitor FPS in-game
3. Check CPU usage (Task Manager)
4. Verify no stuttering or frame drops
5. Test during 20+ minute session

---

## 📊 Performance Targets

| Metric | Target | Acceptable | Critical |
|--------|--------|------------|----------|
| **Overlay FPS** | 60 FPS | 50+ FPS | <45 FPS |
| **CPU Usage** | <3% | <5% | >8% |
| **Memory Usage** | <80MB | <100MB | >150MB |
| **iRacing FPS Impact** | <2% | <5% | >10% |
| **UI Latency** | <16ms | <33ms | >50ms |

---

## ✨ Completion Checklist

Before moving to MVP 3, ensure:

- [ ] All success criteria met
- [ ] Overlay displays correctly over iRacing
- [ ] All 5 metrics update in real-time
- [ ] Window can be repositioned
- [ ] No performance impact on iRacing
- [ ] Code is committed to Git
- [ ] Screenshots/video captured for documentation
- [ ] Performance targets achieved

---

## 🚀 Next Steps

Once MVP 2 is complete:

1. Review this document and check all boxes
2. Update status to 🟢 Complete
3. Take screenshots of working overlay
4. Commit all code changes
5. Move to **MVP 3: First Production Widget**

---

## 📚 Resources

- [WPF Documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [WPF Transparent Windows](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/how-to-create-a-transparent-window)
- [Hardware Acceleration in WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/graphics-rendering-overview)

---

**Created:** October 12, 2025  
**Last Updated:** October 12, 2025  
**Next Review:** After completion  
**Related:** [MVP 1](./MVP1_Basic_Connection.md) | [MVP 3](./MVP3_First_Widget.md) | [Overview](../../MANAGEABLE_PHASES.md)
