using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF.Widgets.DataWidget;

/// <summary>
/// 2x3 grid widget displaying customizable telemetry data cells
/// Each cell can display any TelemetryField with label
/// </summary>
public class DataWidget : WidgetBase
{
    private readonly Grid _mainGrid;
    private readonly Border _border;
    private readonly System.Windows.Media.Color _primaryColor = System.Windows.Media.Color.FromRgb(0, 128, 128); // Teal #008080
    private readonly System.Windows.Media.Color _secondaryColor = System.Windows.Media.Color.FromRgb(255, 128, 0); // Orange #FF8000
    
    // Store cell components
    private readonly List<DataCell> _dataCells = new();

    public override WidgetType WidgetType => WidgetType.Data;

    public DataWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        Title = "Data Widget";
        Width = 420;  // Slightly wider to fit tire data better
        Height = 300;

        // Subscribe to settings changes to update temperature unit indicators
        AppSettings.Instance.SettingsChanged += OnSettingsChanged;

        // Create main grid (2 columns x 3 rows) with fixed design dimensions
        _mainGrid = new Grid
        {
            Background = Brushes.Transparent,
            Width = 420,
            Height = 300
        };

        // Define columns (fixed width for better text fitting)
        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });

        // Define rows (fixed height)
        _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });
        _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });
        _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(85) });

        // Create 6 data cells (2x3 grid)
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                var cell = CreateDataCell(row, col);
                _dataCells.Add(cell);
            }
        }

        // Border with rounded corners
        _border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(221, 0, 0, 0)), // #DD000000
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10),
            BorderBrush = new SolidColorBrush(_primaryColor),
            BorderThickness = new Thickness(2),
            Child = _mainGrid
        };

        Content = _border;
        
        // Subscribe to SizeChanged to update scale transform
        SizeChanged += OnWidgetSizeChanged;
        
        // Initialize with all cells collapsed since they all start as None
        UpdateGridLayout();
    }
    
    /// <summary>
    /// Handle widget resize by scaling the content via LayoutTransform.
    /// This prevents content shift by maintaining relative positions of all elements.
    /// </summary>
    private void OnWidgetSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Calculate scale factors based on original 420x300 design
        double scaleX = ActualWidth / 420.0;
        double scaleY = ActualHeight / 300.0;
        
        // Use uniform scale (smallest of the two to maintain aspect ratio)
        double scale = Math.Min(scaleX, scaleY);
        
        // Apply scale transform to the entire grid
        _mainGrid.LayoutTransform = new ScaleTransform(scale, scale);
    }

    /// <summary>
    /// Update cell fields with new telemetry field selections
    /// Empty cells (None) will be automatically hidden
    /// </summary>
    public void UpdateCellFields(TelemetryField field1, TelemetryField field2, TelemetryField field3, 
                                 TelemetryField field4, TelemetryField field5, TelemetryField field6)
    {
        if (_dataCells.Count != 6)
            return;

        // Update each cell's field and visibility
        UpdateCellFieldAndVisibility(_dataCells[0], field1);
        UpdateCellFieldAndVisibility(_dataCells[1], field2);
        UpdateCellFieldAndVisibility(_dataCells[2], field3);
        UpdateCellFieldAndVisibility(_dataCells[3], field4);
        UpdateCellFieldAndVisibility(_dataCells[4], field5);
        UpdateCellFieldAndVisibility(_dataCells[5], field6);
    }

    /// <summary>
    /// Update a single cell's field and hide if None
    /// </summary>
    private void UpdateCellFieldAndVisibility(DataCell cell, TelemetryField field)
    {
        cell.Field = field;
        UpdateCellLabel(cell);
        
        // Hide the entire cell container if field is None
        cell.Container.Visibility = (field == TelemetryField.None) ? Visibility.Collapsed : Visibility.Visible;
        
        // Also hide the field selector when not None
        cell.FieldSelector.Visibility = Visibility.Collapsed;
        
        // Update grid layout to collapse empty rows/columns
        UpdateGridLayout();
    }
    
    /// <summary>
    /// Update grid layout and widget size based on visible cells
    /// Hides empty columns/rows and adjusts widget dimensions dynamically
    /// </summary>
    private void UpdateGridLayout()
    {
        // Check which rows and columns have visible cells
        bool[] rowHasVisibleCells = new bool[3];
        bool[] colHasVisibleCells = new bool[2];
        
        foreach (var cell in _dataCells)
        {
            if (cell.Field != TelemetryField.None && cell.Container.Visibility == Visibility.Visible)
            {
                rowHasVisibleCells[cell.Row] = true;
                colHasVisibleCells[cell.Column] = true;
            }
        }
        
        // Hide/show columns based on visibility
        for (int col = 0; col < 2; col++)
        {
            if (colHasVisibleCells[col])
            {
                _mainGrid.ColumnDefinitions[col].Width = new GridLength(190);
            }
            else
            {
                _mainGrid.ColumnDefinitions[col].Width = new GridLength(0);
            }
        }
        
        // Hide/show rows based on visibility
        for (int row = 0; row < 3; row++)
        {
            if (rowHasVisibleCells[row])
            {
                _mainGrid.RowDefinitions[row].Height = new GridLength(85);
            }
            else
            {
                _mainGrid.RowDefinitions[row].Height = new GridLength(0);
            }
        }
        
        // Count visible rows and columns
        int visibleRows = rowHasVisibleCells.Count(x => x);
        int visibleColumns = colHasVisibleCells.Count(x => x);
        
        // Adjust widget size based on visible cells
        // Base cell size: 190 wide x 85 tall
        // Plus border/padding: 20px padding (10 per side) + 4px border (2 per side) = 24px
        const double cellWidth = 190;
        const double cellHeight = 85;
        const double borderPadding = 24;
        
        if (visibleColumns > 0 && visibleRows > 0)
        {
            double newWidth = (cellWidth * visibleColumns) + borderPadding;
            double newHeight = (cellHeight * visibleRows) + borderPadding;
            
            Width = newWidth;
            Height = newHeight;
        }
        else
        {
            // No visible cells, use minimum size
            Width = 220;
            Height = 100;
        }
    }

    /// <summary>
    /// Create a single data cell with label, value, and field selector
    /// </summary>
    private DataCell CreateDataCell(int row, int col)
    {
        var cell = new DataCell
        {
            Row = row,
            Column = col,
            Field = TelemetryField.None
        };

        // Create cell container
        var cellBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(100, 20, 20, 20)),
            CornerRadius = new CornerRadius(5),
            Margin = new Thickness(5),
            Padding = new Thickness(8),
            Visibility = Visibility.Collapsed  // Start hidden since field is None
        };

        // Create vertical stack for label and value
        var cellStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // ComboBox for field selection (shown on hover/edit mode)
        var fieldSelector = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(TelemetryField)).Cast<TelemetryField>().ToList(),
            SelectedItem = TelemetryField.None,
            Margin = new Thickness(0, 0, 0, 5),
            Visibility = Visibility.Collapsed // Hidden by default
        };
        fieldSelector.SelectionChanged += (s, e) =>
        {
            if (fieldSelector.SelectedItem is TelemetryField field)
            {
                cell.Field = field;
                UpdateCellLabel(cell);
            }
        };
        cell.FieldSelector = fieldSelector;

        // Label (field name)
        var label = new TextBlock
        {
            Text = "---",
            Foreground = new SolidColorBrush(_primaryColor),
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        cell.Label = label;

        // Value text (reduced size to fit tire data)
        var value = new TextBlock
        {
            Text = "---",
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 16,  // Reduced from 20 to fit TireWearAll data better
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap, // Allow multi-line text (e.g., tire temps)
            MaxWidth = 175  // Prevent text from getting too wide
        };
        cell.Value = value;

        cellStack.Children.Add(fieldSelector);
        cellStack.Children.Add(label);
        cellStack.Children.Add(value);

        cellBorder.Child = cellStack;

        // Add context menu for field selection
        var contextMenu = new ContextMenu();
        var selectFieldItem = new MenuItem { Header = "Select Field" };
        selectFieldItem.Click += (s, e) =>
        {
            // Toggle field selector visibility
            fieldSelector.Visibility = fieldSelector.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        };
        contextMenu.Items.Add(selectFieldItem);
        cellBorder.ContextMenu = contextMenu;

        // Add to grid
        Grid.SetRow(cellBorder, row);
        Grid.SetColumn(cellBorder, col);
        _mainGrid.Children.Add(cellBorder);

        cell.Container = cellBorder;

        return cell;
    }

    /// <summary>
    /// Update cell label text based on selected field
    /// </summary>
    private void UpdateCellLabel(DataCell cell)
    {
        cell.Label.Text = cell.Field == TelemetryField.None
            ? "---"
            : FormatFieldName(cell.Field);
    }

    /// <summary>
    /// Format field name for display (e.g., "SpeedKmh" -> "SPEED KM/H")
    /// Adds unit indicators for temperature fields based on global units setting
    /// </summary>
    private string FormatFieldName(TelemetryField field)
    {
        var name = field.ToString();
        
        // Add spaces before capitals
        var spaced = string.Concat(name.Select((x, i) =>
            i > 0 && char.IsUpper(x) ? " " + x : x.ToString()));
        
        var formattedName = spaced.ToUpper();
        
        // Add unit indicators for temperature fields based on global units setting
        var tempUnit = AppSettings.Instance.UseMetricUnits ? " (°C)" : " (°F)";
        
        return field switch
        {
            TelemetryField.WaterTemp => formattedName + tempUnit,
            TelemetryField.OilTemp => formattedName + tempUnit,
            TelemetryField.AirTemp => formattedName + tempUnit,
            TelemetryField.TrackTemp => formattedName + tempUnit,
            TelemetryField.TireTempLF => formattedName + tempUnit,
            TelemetryField.TireTempRF => formattedName + tempUnit,
            TelemetryField.TireTempLR => formattedName + tempUnit,
            TelemetryField.TireTempRR => formattedName + tempUnit,
            TelemetryField.TireTempAll => "TIRE TEMP" + tempUnit,
            _ => formattedName
        };
    }

    protected override void UpdateUI(TelemetryData data)
    {
        // Update each cell with current telemetry data
        foreach (var cell in _dataCells)
        {
            if (cell.Field == TelemetryField.None)
            {
                cell.Value.Text = "---";
                cell.Value.Foreground = new SolidColorBrush(Colors.Gray);
                continue;
            }

            // Get value for the selected field
            var value = TelemetryDataMapper.GetValue(cell.Field, data);
            var (valueText, color) = FormatFieldValueAndColor(cell.Field, value, data);
            
            cell.Value.Text = valueText;
            cell.Value.Foreground = new SolidColorBrush(color);
        }
    }

    /// <summary>
    /// Format field value and determine color
    /// </summary>
    private (string text, System.Windows.Media.Color color) FormatFieldValueAndColor(TelemetryField field, object? value, TelemetryData data)
    {
        if (value == null)
            return ("---", Colors.Gray);

        Color color = _primaryColor; // Default teal
        string text;

        switch (field)
        {
            case TelemetryField.Gear:
                if (value is int gear)
                {
                    text = gear switch
                    {
                        -1 => "R",
                        0 => "N",
                        _ => gear.ToString()
                    };
                    color = gear switch
                    {
                        -1 => Colors.Red,
                        0 => Colors.Gray,
                        _ => _primaryColor
                    };
                }
                else
                {
                    text = "---";
                }
                break;

            case TelemetryField.Speed:
            case TelemetryField.SpeedKmh:
            case TelemetryField.SpeedMph:
                if (value is float speedMs)
                {
                    if (field == TelemetryField.SpeedMph || !AppSettings.Instance.UseMetricUnits)
                    {
                        text = $"{(int)(speedMs * 2.23694f)}"; // m/s to mph
                    }
                    else
                    {
                        text = $"{(int)(speedMs * 3.6f)}"; // m/s to km/h
                    }
                }
                else
                {
                    text = "0";
                }
                break;

            case TelemetryField.RPM:
                text = value is float rpm ? $"{(int)rpm}" : "0";
                // Color coding based on RPM zones (Yellow -> Orange -> Red)
                if (value is float rpmVal)
                {
                    var zone = ShiftPointCalculator.GetRPMZone(rpmVal, data.Gear);
                    color = zone switch
                    {
                        ShiftPointCalculator.RPMZone.Danger => Colors.Red,         // RED - at limiter
                        ShiftPointCalculator.RPMZone.Optimal => _secondaryColor,   // ORANGE - optimal shift
                        ShiftPointCalculator.RPMZone.Warning => Colors.Yellow,     // YELLOW - approaching shift
                        _ => _primaryColor                                          // TEAL - safe range
                    };
                }
                break;

            case TelemetryField.Throttle:
            case TelemetryField.Brake:
            case TelemetryField.Clutch:
                text = value is float percent ? $"{(int)(percent * 100)}%" : "0%";
                break;

            case TelemetryField.FuelLevel:
            case TelemetryField.FuelPercent:
                if (value is float fuel)
                {
                    if (field == TelemetryField.FuelPercent)
                    {
                        text = $"{(int)(fuel * 100)}%";
                        // Color warnings for low fuel percentage
                        if (fuel < 0.1f)
                            color = Colors.Red;
                        else if (fuel < 0.2f)
                            color = Colors.Yellow;
                    }
                    else
                    {
                        // Convert fuel based on unit setting (liters or gallons)
                        if (AppSettings.Instance.UseMetricUnits)
                        {
                            text = $"{fuel:F1}L";
                            // Color warnings for low fuel (liters)
                            if (fuel < 5f)
                                color = Colors.Red;
                            else if (fuel < 10f)
                                color = Colors.Yellow;
                        }
                        else
                        {
                            float fuelGallons = fuel * 0.264172f; // liters to gallons
                            text = $"{fuelGallons:F1}gal";
                            // Color warnings for low fuel (gallons)
                            if (fuelGallons < 1.3f)
                                color = Colors.Red;
                            else if (fuelGallons < 2.6f)
                                color = Colors.Yellow;
                        }
                    }
                }
                else
                {
                    text = "---";
                }
                break;

            case TelemetryField.WaterTemp:
            case TelemetryField.OilTemp:
                if (value is float temp)
                {
                    text = AppSettings.Instance.UseMetricUnits ? $"{(int)temp}°C" : $"{(int)(temp * 9 / 5 + 32)}°F";
                    // Color warnings for high temps (based on Celsius values)
                    if (temp > 110f)
                        color = Colors.Red;
                    else if (temp > 100f)
                        color = Colors.Yellow;
                }
                else
                {
                    text = "---";
                }
                break;

            case TelemetryField.TireTempLF:
            case TelemetryField.TireTempRF:
            case TelemetryField.TireTempLR:
            case TelemetryField.TireTempRR:
                if (value is float tireTemp)
                {
                    text = AppSettings.Instance.UseMetricUnits ? $"{(int)tireTemp}°C" : $"{(int)(tireTemp * 9 / 5 + 32)}°F";
                    
                    // Color coding based on tire temperature ranges (Celsius)
                    // Cold: < 60°C (blue)
                    // Warming: 60-75°C (teal)
                    // Optimal: 75-95°C (green)
                    // Hot: 95-110°C (orange)
                    // Extreme: > 110°C (red)
                    color = tireTemp switch
                    {
                        < 60f => Colors.Blue,        // Cold
                        < 75f => Colors.Teal,        // Warming
                        < 95f => Colors.LightGreen,  // Optimal
                        < 110f => Colors.Orange,     // Hot
                        _ => Colors.Red              // Extreme
                    };
                }
                else
                {
                    text = "---";
                }
                break;

            case TelemetryField.TireTempAll:
                // Multi-line display: "FL xx xx RF\nLR xx xx RR"
                // Row 1: FL [left-front] [right-front] RF
                // Row 2: LR [left-rear] [right-rear] RR
                if (value is string allTemps)
                {
                    text = allTemps; // Keep newline for multi-line display
                    
                    // For TireTempAll, get average temp for color coding from individual tire values
                    var lf = TelemetryDataMapper.GetValue(TelemetryField.TireTempLF, data) as float? ?? 0f;
                    var rf = TelemetryDataMapper.GetValue(TelemetryField.TireTempRF, data) as float? ?? 0f;
                    var lr = TelemetryDataMapper.GetValue(TelemetryField.TireTempLR, data) as float? ?? 0f;
                    var rr = TelemetryDataMapper.GetValue(TelemetryField.TireTempRR, data) as float? ?? 0f;
                    float avgTemp = (lf + rf + lr + rr) / 4f;
                    
                    color = avgTemp switch
                    {
                        < 60f => Colors.Blue,        // Cold
                        < 75f => Colors.Teal,        // Warming
                        < 95f => Colors.LightGreen,  // Optimal
                        < 110f => Colors.Orange,     // Hot
                        _ => Colors.Red              // Extreme
                    };
                }
                else
                {
                    text = "FL -- -- RF\nLR -- -- RR";
                }
                break;

            case TelemetryField.TireWearAll:
                // Multi-line display: "FL xx% xx% RF\nLR xx% xx% RR"
                // Row 1: FL [left-front%] [right-front%] RF
                // Row 2: LR [left-rear%] [right-rear%] RR
                if (value is string allWear)
                {
                    text = allWear; // Keep newline for multi-line display
                    System.Diagnostics.Debug.WriteLine($"TireWearAll STRING: {allWear}");
                }
                else
                {
                    text = "FL --% --% RF\nLR --% --% RR";
                    System.Diagnostics.Debug.WriteLine($"TireWearAll FALLBACK (value type: {value?.GetType().Name ?? "null"})");
                }
                break;
            
            case TelemetryField.TireWearLF:
            case TelemetryField.TireWearRF:
            case TelemetryField.TireWearLR:
            case TelemetryField.TireWearRR:
                // Individual tire wear (percentage) - color code based on remaining tire life
                if (value is float wearPercent)
                {
                    text = $"{(int)wearPercent}%";
                    
                    // Color coding: lower % = more worn = more critical
                    // 50% yellow, 35% orange, 20% red
                    color = wearPercent switch
                    {
                        <= 20f => Color.FromRgb(255, 51, 51),   // Red #FF3333
                        <= 35f => Color.FromRgb(255, 128, 0),   // Orange #FF8000
                        <= 50f => Colors.Yellow,                 // Yellow #FFFF00
                        _ => _primaryColor                       // Teal (good condition)
                    };
                }
                else
                {
                    text = "---";
                }
                break;

            case TelemetryField.LapNumber:
            case TelemetryField.Position:
            case TelemetryField.ClassPosition:
                text = value?.ToString() ?? "---";
                break;

            case TelemetryField.SessionTimeRemaining:
            case TelemetryField.SessionTime:
                // Format as time (HH:mm:ss or mm:ss depending on duration)
                if (value is double sessionTime && sessionTime > 0)
                {
                    var ts = TimeSpan.FromSeconds(sessionTime);
                    // For session time >= 1 hour, show HH:mm:ss
                    if (sessionTime >= 3600)
                        text = $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
                    // For session time < 1 hour, show mm:ss
                    else
                        text = $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
                }
                else
                {
                    text = "--:--";
                }
                break;

            case TelemetryField.LastLapTime:
            case TelemetryField.BestLapTime:
            case TelemetryField.CurrentLapTime:
                if (value is float time && time > 0)
                {
                    var ts = TimeSpan.FromSeconds(time);
                    text = ts.ToString(@"m\:ss\.fff");
                }
                else
                {
                    text = "--:--.---";
                }
                break;

            default:
                // Generic formatting for other fields (use InvariantCulture for consistent decimal separator)
                text = value switch
                {
                    float f => f.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                    int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    bool b => b ? "YES" : "NO",
                    string s => s,
                    _ => value.ToString() ?? "---"
                };
                break;
        }

        return (text, color);
    }

    /// <summary>
    /// Update widget size and scale all elements proportionally (live update)
    /// </summary>
    public void UpdateSize(double width, double height)
    {
        Width = width;
        Height = height;
        
        // Calculate scale factors based on default size of 420x300 (with fixed cells)
        double widthScale = width / 420.0;
        double heightScale = height / 300.0;
        double avgScale = (widthScale + heightScale) / 2.0;
        
        // Scale border properties
        _border.Padding = new Thickness(10 * avgScale);
        _border.BorderThickness = new Thickness(2 * avgScale);
        _border.CornerRadius = new CornerRadius(10 * avgScale);
        
        // Scale grid column widths (base: 190px per column)
        foreach (var colDef in _mainGrid.ColumnDefinitions)
        {
            colDef.Width = new GridLength(190 * widthScale);
        }
        
        // Scale grid row heights (base: 85px per row)
        foreach (var rowDef in _mainGrid.RowDefinitions)
        {
            rowDef.Height = new GridLength(85 * heightScale);
        }
        
        // Scale cell sizes and fonts
        foreach (var cell in _dataCells)
        {
            // Scale cell border properties
            cell.Container.Margin = new Thickness(5 * avgScale);
            cell.Container.Padding = new Thickness(8 * avgScale);
            if (cell.Container.CornerRadius.TopLeft > 0)
                cell.Container.CornerRadius = new CornerRadius(5 * avgScale);
            
            // Scale label font size (default 10px)
            cell.Label.FontSize = 10 * avgScale;
            
            // Scale value font size (default 20px - reduced for better fit)
            cell.Value.FontSize = 20 * avgScale;
            cell.Value.MaxWidth = 175 * avgScale;  // Scale max width too
            
            // Scale field selector (if visible)
            cell.FieldSelector.FontSize = 10 * avgScale;
        }
    }

    /// <summary>
    /// Handle settings changes (e.g., unit system change)
    /// </summary>
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Update all cell labels to reflect new unit indicators
        foreach (var cell in _dataCells)
        {
            UpdateCellLabel(cell);
        }
    }

    /// <summary>
    /// Internal class to track cell components
    /// </summary>
    private class DataCell
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public TelemetryField Field { get; set; }
        public Border Container { get; set; } = null!;
        public System.Windows.Controls.ComboBox FieldSelector { get; set; } = null!;
        public TextBlock Label { get; set; } = null!;
        public TextBlock Value { get; set; } = null!;
    }
}
