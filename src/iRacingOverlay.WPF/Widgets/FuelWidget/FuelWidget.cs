using System;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.FuelWidget;

/// <summary>
/// Fuel Calculator Widget — compact overlay showing fuel saving / lift &amp; coast data.
/// Displays: Fuel Remaining, L/Lap, Laps Left, L3/L5 averages, splutter buffer,
/// Projected Delta, Saving Target, Strategy.
/// MRT theme: dark background, teal/orange accents, high contrast text.
/// </summary>
public partial class FuelWidget : WidgetBase
{
    #region Constants

    private const double WIDGET_WIDTH = 210;
    private const double BASE_HEIGHT = 68;
    private const double PADDING = 8;
    private const double ROW_HEIGHT = 18;
    private const double BORDER_RADIUS = 6;

    #endregion

    #region Colors

    private static readonly Color COLOR_TEAL = Color.FromRgb(0, 128, 128);
    private static readonly Color COLOR_ORANGE = Color.FromRgb(255, 128, 0);
    private static readonly Color COLOR_DARK_BG = Color.FromArgb(245, 18, 18, 18);
    private static readonly Color COLOR_TEXT = Color.FromRgb(185, 185, 185);
    private static readonly Color COLOR_MUTED = Color.FromRgb(120, 120, 120);
    private static readonly Color COLOR_GREEN = Color.FromRgb(0, 200, 83);
    private static readonly Color COLOR_YELLOW = Color.FromRgb(255, 255, 0);
    private static readonly Color COLOR_RED = Color.FromRgb(255, 50, 50);
    private static readonly Color COLOR_DIM = Color.FromRgb(60, 60, 60);

    private static readonly SolidColorBrush BRUSH_TEAL = new(COLOR_TEAL);
    private static readonly SolidColorBrush BRUSH_ORANGE = new(COLOR_ORANGE);
    private static readonly SolidColorBrush BRUSH_TEXT = new(COLOR_TEXT);
    private static readonly SolidColorBrush BRUSH_MUTED = new(COLOR_MUTED);
    private static readonly SolidColorBrush BRUSH_GREEN = new(COLOR_GREEN);
    private static readonly SolidColorBrush BRUSH_YELLOW = new(COLOR_YELLOW);
    private static readonly SolidColorBrush BRUSH_RED = new(COLOR_RED);
    private static readonly SolidColorBrush BRUSH_DIM = new(COLOR_DIM);

    #endregion

    #region UI Elements

    private Canvas _canvas = null!;
    private Border _backgroundBorder = null!;
    private StackPanel _mainStack = null!;

    // Core rows
    private TextBlock _valFuelLevel = null!;
    private TextBlock _valFuelPct = null!;
    private TextBlock _valLPerLap = null!;
    private TextBlock _valLapsLeft = null!;

    // Toggleable average rows
    private Grid _rowL3 = null!;
    private TextBlock _valL3 = null!;
    private Grid _rowL5 = null!;
    private TextBlock _valL5 = null!;

    // Splutter buffer row
    private Grid _rowBuffer = null!;
    private TextBlock _valBuffer = null!;

    // Predicted pit lap row
    private Grid _rowPitLap = null!;
    private TextBlock _valPitLap = null!;

    // Fill amount calculator row
    private Grid _rowFillAmount = null!;
    private TextBlock _valFillAmount = null!;

    // Pit window row
    private Grid _rowPitWindow = null!;
    private TextBlock _valPitWindow = null!;

    // Full-tank stint length row
    private Grid _rowTankLaps = null!;
    private TextBlock _valTankLaps = null!;

    // Saving section
    private TextBlock _valDelta = null!;
    private TextBlock _valSavingTarget = null!;
    private TextBlock _valSavingRate = null!;
    private TextBlock _valStrategy = null!;

    // Toggleable section containers
    private StackPanel _tankPctContainer = null!;
    private StackPanel _savingContainer = null!;
    private StackPanel _alertContainer = null!;

    // Alert bar
    private TextBlock _alertText = null!;
    private Border _alertBorder = null!;

    #endregion

    #region Settings

    /// <summary>Show Last 3 lap average row</summary>
    public bool ShowL3Average { get; set; } = true;

    /// <summary>Show Last 5 lap average row</summary>
    public bool ShowL5Average { get; set; } = true;

    /// <summary>Show splutter buffer row (hidden by default — buffer is used in background calculations)</summary>
    public bool ShowBuffer { get; set; } = false;

    /// <summary>Show fuel saving section (delta, target, rate, strategy)</summary>
    public bool ShowSavingSection { get; set; } = true;

    /// <summary>Show tank percentage row</summary>
    public bool ShowTankPct { get; set; } = true;

    /// <summary>Show alert bar</summary>
    public bool ShowAlert { get; set; } = true;

    /// <summary>Show predicted pit lap row</summary>
    public bool ShowPitLap { get; set; } = false;

    /// <summary>Show fill amount calculator row (fuel needed to finish)</summary>
    public bool ShowFillAmount { get; set; } = false;

    /// <summary>Show optimal pit window row (lap range to pit)</summary>
    public bool ShowPitWindow { get; set; } = true;

    /// <summary>Show how many laps a full tank covers at the current average.</summary>
    public bool ShowTankLaps { get; set; } = false;

    /// <summary>Show the ▲/▼ consumption trend beside L/LAP.</summary>
    public bool ShowTrend { get; set; } = true;

    /// <summary>Buffer laps for fuel calculation (synced with MRT One settings)</summary>
    public float BufferLaps { get; set; } = 1.0f;

    /// <summary>Manual save target override (L/lap reduction). 0 = auto-calculate.</summary>
    public float ManualSaveTarget { get; set; } = 0f;

    #endregion

    public override WidgetType WidgetType => WidgetType.FuelCalculator;

    /// <summary>Update at 6Hz (every 10th tick) — fuel level decreases gradually.</summary>
    protected override int UpdateIntervalTicks => 10;

    public FuelWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        Title = "Fuel Calculator";
        Width = WIDGET_WIDTH;

        LoadSettings();
        InitializeWidget();
        RecalcHeight();
    }

    private void LoadSettings()
    {
        if (Config?.Settings == null) return;

        ShowL3Average = ReadBool("showL3Average", ShowL3Average);
        ShowL5Average = ReadBool("showL5Average", ShowL5Average);
        ShowBuffer = ReadBool("showBuffer", ShowBuffer);
        ShowSavingSection = ReadBool("showSavingSection", ShowSavingSection);
        ShowTankPct = ReadBool("showTankPct", ShowTankPct);
        ShowAlert = ReadBool("showAlert", ShowAlert);
        ShowPitLap = ReadBool("showPitLap", ShowPitLap);
        ShowFillAmount = ReadBool("showFillAmount", ShowFillAmount);
        ShowPitWindow = ReadBool("showPitWindow", ShowPitWindow);
        ShowTankLaps = ReadBool("showTankLaps", ShowTankLaps);
        ShowTrend = ReadBool("showTrend", ShowTrend);
        ManualSaveTarget = ReadFloat("manualSaveTarget", ManualSaveTarget);
    }

    /// <summary>
    /// Settings arrive as JsonElement from a loaded layout and as bool once set
    /// in-process; both shapes must read the same.
    /// </summary>
    private bool ReadBool(string key, bool fallback)
    {
        if (!Config.Settings!.TryGetValue(key, out var v)) return fallback;
        return v switch
        {
            JsonElement je => je.ValueKind == JsonValueKind.True,
            bool b => b,
            _ => fallback,
        };
    }

    private float ReadFloat(string key, float fallback)
    {
        if (!Config.Settings!.TryGetValue(key, out var v)) return fallback;
        return v switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.Number => (float)je.GetDouble(),
            float f => f,
            double d => (float)d,
            _ => fallback,
        };
    }

    protected override void SaveWidgetSettings() => SaveSettings();

    public void SaveSettings()
    {
        Config.Settings ??= new Dictionary<string, object>();
        Config.Settings["showL3Average"] = ShowL3Average;
        Config.Settings["showL5Average"] = ShowL5Average;
        Config.Settings["showBuffer"] = ShowBuffer;
        Config.Settings["showSavingSection"] = ShowSavingSection;
        Config.Settings["showTankPct"] = ShowTankPct;
        Config.Settings["showAlert"] = ShowAlert;
        Config.Settings["showPitLap"] = ShowPitLap;
        Config.Settings["showFillAmount"] = ShowFillAmount;
        Config.Settings["showPitWindow"] = ShowPitWindow;
        Config.Settings["showTankLaps"] = ShowTankLaps;
        Config.Settings["showTrend"] = ShowTrend;
        Config.Settings["manualSaveTarget"] = ManualSaveTarget;
    }

    private void InitializeWidget()
    {
        _canvas = new Canvas
        {
            Width = WIDGET_WIDTH,
            ClipToBounds = false
        };
        Content = _canvas;

        _backgroundBorder = new Border
        {
            Width = WIDGET_WIDTH,
            CornerRadius = new CornerRadius(BORDER_RADIUS),
            Background = new SolidColorBrush(COLOR_DARK_BG),
            BorderBrush = BRUSH_TEAL,
            BorderThickness = new Thickness(1.5),
            Effect = new DropShadowEffect
            {
                Color = COLOR_TEAL,
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.3
            }
        };
        _canvas.Children.Add(_backgroundBorder);

        _mainStack = new StackPanel { Margin = new Thickness(PADDING, PADDING, PADDING, 4) };
        _backgroundBorder.Child = _mainStack;

        // Header
        _mainStack.Children.Add(new TextBlock
        {
            Text = "FUEL",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = BRUSH_TEAL,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        _mainStack.Children.Add(CreateSeparator());

        // Core rows
        _valFuelLevel = CreateRow("FUEL", out _);

        // Toggleable TANK % row — wrapped in container
        _tankPctContainer = new StackPanel();
        _valFuelPct = CreateRow("TANK %", out _, _tankPctContainer);
        _mainStack.Children.Add(_tankPctContainer);
        _tankPctContainer.Visibility = ShowTankPct ? Visibility.Visible : Visibility.Collapsed;

        _valLPerLap = CreateRow("L/LAP", out _);
        _valLapsLeft = CreateRow("LAPS LEFT", out _);

        // Toggleable L3 average
        _valL3 = CreateToggleRow("AVG L3", out _rowL3);
        _rowL3.Visibility = ShowL3Average ? Visibility.Visible : Visibility.Collapsed;

        // Toggleable L5 average
        _valL5 = CreateToggleRow("AVG L5", out _rowL5);
        _rowL5.Visibility = ShowL5Average ? Visibility.Visible : Visibility.Collapsed;

        // Splutter buffer row
        _valBuffer = CreateToggleRow("BUFFER", out _rowBuffer);
        _rowBuffer.Visibility = ShowBuffer ? Visibility.Visible : Visibility.Collapsed;

        // Predicted pit lap row
        _valPitLap = CreateToggleRow("PIT LAP", out _rowPitLap);
        _rowPitLap.Visibility = ShowPitLap ? Visibility.Visible : Visibility.Collapsed;

        // Fill amount calculator row
        _valFillAmount = CreateToggleRow("FILL AMT", out _rowFillAmount);
        _rowFillAmount.Visibility = ShowFillAmount ? Visibility.Visible : Visibility.Collapsed;

        // Pit window row (optimal lap range to pit)
        _valPitWindow = CreateToggleRow("PIT WINDOW", out _rowPitWindow);
        _rowPitWindow.Visibility = ShowPitWindow ? Visibility.Visible : Visibility.Collapsed;

        // Full-tank stint length
        _valTankLaps = CreateToggleRow("TANK LAPS", out _rowTankLaps);
        _rowTankLaps.Visibility = ShowTankLaps ? Visibility.Visible : Visibility.Collapsed;

        // Toggleable saving section (separator + 4 rows)
        _savingContainer = new StackPanel();
        _savingContainer.Children.Add(CreateSeparator());
        _valDelta = CreateRow("PROJ DELTA", out _, _savingContainer);
        _valSavingTarget = CreateRow("SAVE TGT", out _, _savingContainer);
        _valSavingRate = CreateRow("SAVING", out _, _savingContainer);
        _valStrategy = CreateRow("STRATEGY", out _, _savingContainer);
        _mainStack.Children.Add(_savingContainer);
        _savingContainer.Visibility = ShowSavingSection ? Visibility.Visible : Visibility.Collapsed;

        // Toggleable alert bar (separator + alert)
        _alertContainer = new StackPanel();
        _alertContainer.Children.Add(CreateSeparator());

        _alertBorder = new Border
        {
            Height = 20,
            CornerRadius = new CornerRadius(3),
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _alertText = new TextBlock
        {
            Text = "",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = BRUSH_TEXT,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _alertBorder.Child = _alertText;
        _alertContainer.Children.Add(_alertBorder);
        _mainStack.Children.Add(_alertContainer);
        _alertContainer.Visibility = ShowAlert ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Recalculate widget height based on visible optional rows.</summary>
    public void RecalcHeight()
    {
        int extraRows = 0;
        if (ShowL3Average) extraRows++;
        if (ShowL5Average) extraRows++;
        if (ShowBuffer) extraRows++;
        if (ShowTankPct) extraRows++;
        if (ShowPitLap) extraRows++;
        if (ShowFillAmount) extraRows++;
        if (ShowPitWindow) extraRows++;
        if (ShowTankLaps) extraRows++;

        // Saving section = separator(5) + 4 rows; alert = separator(5) + alert bar(22)
        double savingHeight = ShowSavingSection ? 5 + (4 * (ROW_HEIGHT + 2)) : 0;
        double alertHeight = ShowAlert ? 27 : 0;

        double h = BASE_HEIGHT + (extraRows * (ROW_HEIGHT + 2)) + savingHeight + alertHeight;
        Height = h;
        _canvas.Height = h;
        _backgroundBorder.Height = h;
        Config.Height = h;
    }

    /// <summary>Apply current toggle states to row visibility and resize.</summary>
    public void ApplyToggles()
    {
        _rowL3.Visibility = ShowL3Average ? Visibility.Visible : Visibility.Collapsed;
        _rowL5.Visibility = ShowL5Average ? Visibility.Visible : Visibility.Collapsed;
        _rowBuffer.Visibility = ShowBuffer ? Visibility.Visible : Visibility.Collapsed;
        _rowPitLap.Visibility = ShowPitLap ? Visibility.Visible : Visibility.Collapsed;
        _rowFillAmount.Visibility = ShowFillAmount ? Visibility.Visible : Visibility.Collapsed;
        _rowPitWindow.Visibility = ShowPitWindow ? Visibility.Visible : Visibility.Collapsed;
        _rowTankLaps.Visibility = ShowTankLaps ? Visibility.Visible : Visibility.Collapsed;
        _tankPctContainer.Visibility = ShowTankPct ? Visibility.Visible : Visibility.Collapsed;
        _savingContainer.Visibility = ShowSavingSection ? Visibility.Visible : Visibility.Collapsed;
        _alertContainer.Visibility = ShowAlert ? Visibility.Visible : Visibility.Collapsed;
        RecalcHeight();
    }

    /// <summary>
    /// " ▲2%" / " ▼3%" after the L/LAP value once the three-lap window drifts more
    /// than a percent from the ten-lap baseline. Below that it is noise, and the
    /// service reports zero until six clean laps exist anyway.
    /// </summary>
    private string TrendSuffix(FuelData fuel)
    {
        if (!ShowTrend) return "";

        float pct = fuel.ConsumptionTrendPct * 100f;
        if (Math.Abs(pct) < 1f) return "";

        return (pct > 0 ? " ▲" : " ▼") + Math.Abs(pct).ToString("F0", CultureInfo.InvariantCulture) + "%";
    }

    private TextBlock CreateRow(string label, out TextBlock labelBlock, Panel? parent = null)
    {
        var grid = new Grid { Height = ROW_HEIGHT, Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = BRUSH_MUTED,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Consolas")
        };
        Grid.SetColumn(labelBlock, 0);
        grid.Children.Add(labelBlock);

        var valueBlock = new TextBlock
        {
            Text = "--",
            FontSize = 13,
            FontWeight = FontWeights.Normal,
            Foreground = BRUSH_TEXT,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            FontFamily = new FontFamily("Consolas")
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);

        (parent ?? _mainStack).Children.Add(grid);
        return valueBlock;
    }

    private TextBlock CreateToggleRow(string label, out Grid grid)
    {
        grid = new Grid { Height = ROW_HEIGHT, Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lbl = new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = BRUSH_TEAL,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Consolas")
        };
        Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);

        var val = new TextBlock
        {
            Text = "--",
            FontSize = 13,
            FontWeight = FontWeights.Normal,
            Foreground = BRUSH_TEXT,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            FontFamily = new FontFamily("Consolas")
        };
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);

        _mainStack.Children.Add(grid);
        return val;
    }

    private static Border CreateSeparator()
    {
        return new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    #region UpdateUI

    protected override void UpdateUI(TelemetryData data)
    {
        var fuel = _telemetryService.CurrentFuelData;
        if (fuel == null) return;

        // ── Basic fuel info ─────────────────────────────────────
        _valFuelLevel.Text = fuel.CurrentFuel.ToString("F3", CultureInfo.InvariantCulture) + " L";
        _valFuelLevel.FontWeight = FontWeights.Normal;
        _valFuelLevel.Foreground = BRUSH_TEXT;

        _valFuelPct.Text = (fuel.FuelPct * 100f).ToString("F0", CultureInfo.InvariantCulture) + "%";
        _valFuelPct.Foreground = fuel.FuelPct switch
        {
            < 0.10f => BRUSH_RED,
            < 0.25f => BRUSH_YELLOW,
            _ => BRUSH_TEXT
        };

        // L/Lap — show the LAST completed lap's actual fuel usage.
        // This is distinct from AVG L5 which shows a rolling 5-lap average.
        // Falls back to blended estimate when no completed laps yet.
        float lPerLap = fuel.AvgFuelPerLap_Last;
        bool usingEstimate = false;
        if (lPerLap <= 0)
        {
            // Use the calculator's effective average (includes SDK early-lap blending)
            if (fuel.EffectiveAvgFuelPerLap > 0)
            {
                lPerLap = fuel.EffectiveAvgFuelPerLap;
                usingEstimate = fuel.LapsCompleted < 1; // mark as estimate until 1 clean lap
            }
            // Last resort: raw SDK estimate
            else if (fuel.SdkFuelEstimate > 0)
            {
                lPerLap = fuel.SdkFuelEstimate;
                usingEstimate = true;
            }
        }
        _valLPerLap.Text = lPerLap > 0
            ? lPerLap.ToString("F3", CultureInfo.InvariantCulture) + (usingEstimate ? " ~est" : "") + TrendSuffix(fuel)
            : "--";
        _valLPerLap.FontWeight = FontWeights.Normal;
        _valLPerLap.Foreground = BRUSH_TEAL;

        // Laps left (already accounts for splutter buffer in calculator)
        float lapsLeft = fuel.LapsRemaining;
        _valLapsLeft.Text = lapsLeft > 0
            ? lapsLeft.ToString("F1", CultureInfo.InvariantCulture)
            : "--";
        _valLapsLeft.FontWeight = FontWeights.Normal;
        _valLapsLeft.Foreground = lapsLeft switch
        {
            < 1f => BRUSH_RED,
            < 3f => BRUSH_YELLOW,
            < 5f => BRUSH_ORANGE,
            _ => BRUSH_TEXT
        };

        // ── Toggleable average rows (dimmed until enough laps) ────
        if (ShowL3Average)
        {
            float l3 = fuel.AvgFuelPerLap_L3;
            bool hasEnoughL3 = fuel.StintLapCount >= 3;
            _valL3.Text = l3 > 0 ? l3.ToString("F3", CultureInfo.InvariantCulture) : "--";
            _valL3.Foreground = hasEnoughL3 ? BRUSH_TEXT : BRUSH_DIM;
        }

        if (ShowL5Average)
        {
            float l5 = fuel.AvgFuelPerLap_L5;
            bool hasEnoughL5 = fuel.StintLapCount >= 5;
            _valL5.Text = l5 > 0 ? l5.ToString("F3", CultureInfo.InvariantCulture) : "--";
            _valL5.Foreground = hasEnoughL5 ? BRUSH_TEXT : BRUSH_DIM;
        }

        // Splutter buffer display (hidden by default — buffer used in background only)
        if (ShowBuffer)
        {
            float threshold = fuel.FuelSputteringThreshold;
            float usable = Math.Max(0, fuel.CurrentFuel - threshold);
            _valBuffer.Text = $"{threshold:F1}L ({usable:F1} usable)";
            _valBuffer.Foreground = usable < 1f ? BRUSH_RED : BRUSH_TEXT;
        }

        // Predicted pit lap (current lap + fuel laps remaining)
        if (ShowPitLap)
        {
            if (fuel.LapsRemaining > 0 && data.Lap > 0)
            {
                int pitLap = data.Lap + (int)Math.Floor(fuel.LapsRemaining);
                _valPitLap.Text = $"Lap {pitLap}";
                _valPitLap.Foreground = BRUSH_MUTED; // not highlighted per user request
            }
            else
            {
                _valPitLap.Text = "--";
                _valPitLap.Foreground = BRUSH_MUTED;
            }
        }

        // Fill amount calculator (fuel needed to finish from current state)
        if (ShowFillAmount)
        {
            if (fuel.HasSufficientData && fuel.FuelToAddAtPit > 0)
            {
                _valFillAmount.Text = fuel.FuelToAddAtPit.ToString("F1", CultureInfo.InvariantCulture) + " L";
                _valFillAmount.Foreground = BRUSH_ORANGE;
            }
            else if (fuel.HasSufficientData && fuel.CanFinishWithoutStop)
            {
                _valFillAmount.Text = "0 L";
                _valFillAmount.Foreground = BRUSH_GREEN;
            }
            else
            {
                _valFillAmount.Text = "--";
                _valFillAmount.Foreground = BRUSH_MUTED;
            }
        }

        // Optimal pit window (lap range to pit)
        if (ShowPitWindow)
        {
            if (!string.IsNullOrEmpty(fuel.PitWindowReason) && !fuel.CanFinishWithoutStop)
            {
                // Three states the driver needs to tell apart at a glance: the
                // window is ahead (orange, shows the range), this is the lap
                // (red, says so), or the last safe lap has already gone by.
                if (data.Lap > fuel.PitWindowEnd)
                {
                    _valPitWindow.Text = "PIT NOW · LATE";
                    _valPitWindow.Foreground = BRUSH_RED;
                }
                else if (data.Lap + 1 >= fuel.PitWindowStart)
                {
                    _valPitWindow.Text = data.Lap >= fuel.PitWindowEnd
                        ? "PIT NOW · LAST LAP"
                        : $"PIT NOW · by L{fuel.PitWindowEnd}";
                    _valPitWindow.Foreground = BRUSH_RED;
                }
                else
                {
                    _valPitWindow.Text = fuel.PitWindowReason;
                    _valPitWindow.Foreground = BRUSH_ORANGE;
                }
            }
            else if (fuel.CanFinishWithoutStop && fuel.HasSufficientData)
            {
                _valPitWindow.Text = "NO STOP";
                _valPitWindow.Foreground = BRUSH_GREEN;
            }
            else
            {
                _valPitWindow.Text = "--";
                _valPitWindow.Foreground = BRUSH_MUTED;
            }
        }

        // Full-tank stint length — the number a strategy is planned around
        if (ShowTankLaps)
        {
            _valTankLaps.Text = fuel.FullTankLaps > 0
                ? fuel.FullTankLaps.ToString("F1", CultureInfo.InvariantCulture)
                : "--";
            _valTankLaps.Foreground = fuel.FullTankLaps > 0 ? BRUSH_TEXT : BRUSH_MUTED;
        }

        // ── Fuel saving section ─────────────────────────────────
        // Projected delta (surplus/deficit at finish, accounting for sputtering)
        // SavingService sets ProjectedFuelDelta; fall back to Calculator's FuelDeltaToFinish
        float projDelta = fuel.ProjectedFuelDelta;
        bool hasDelta = fuel.HasSufficientData && (fuel.RaceLapsRemaining > 0 || fuel.IsTimedSession || fuel.EstimatedTotalRaceLaps > 0);
        if (hasDelta && Math.Abs(projDelta) < 0.001f && Math.Abs(fuel.FuelDeltaToFinish) > 0.001f)
        {
            // SavingService didn't compute a delta but Calculator did — use that
            projDelta = fuel.FuelDeltaToFinish;
        }
        if (hasDelta)
        {
            string sign = projDelta >= 0 ? "+" : "";
            _valDelta.Text = sign + projDelta.ToString("F2", CultureInfo.InvariantCulture) + " L";
            _valDelta.FontWeight = FontWeights.Normal;
            _valDelta.Foreground = projDelta switch
            {
                >= 1f => BRUSH_GREEN,
                >= 0 => BRUSH_YELLOW,
                _ => BRUSH_RED
            };
        }
        else
        {
            _valDelta.Text = "--";
            _valDelta.FontWeight = FontWeights.Normal;
            _valDelta.Foreground = BRUSH_MUTED;
        }

        // Saving target (L/lap reduction needed)
        // Use manual override if set, otherwise auto-calculated
        float savingTarget = ManualSaveTarget > 0 ? ManualSaveTarget : fuel.FuelSavingTarget;
        bool needsSaving = ManualSaveTarget > 0 || fuel.NeedsFuelSaving;
        if (needsSaving && savingTarget > 0)
        {
            string prefix = ManualSaveTarget > 0 ? "*-" : "-";
            _valSavingTarget.Text = prefix + savingTarget.ToString("F3", CultureInfo.InvariantCulture) + " L";
            _valSavingTarget.FontWeight = FontWeights.Normal;
            _valSavingTarget.Foreground = fuel.CanSaveFuelToFinish ? BRUSH_ORANGE : BRUSH_RED;
        }
        else
        {
            _valSavingTarget.Text = fuel.HasSufficientData ? "OK" : "--";
            _valSavingTarget.FontWeight = FontWeights.Normal;
            _valSavingTarget.Foreground = fuel.HasSufficientData ? BRUSH_GREEN : BRUSH_MUTED;
        }

        // Current saving rate — shows real-time saving from current lap vs L3 average.
        // Positive = driver is saving fuel (lifting/coasting). Visible anytime there's data,
        // not just when NeedsFuelSaving — gives instant feedback on lift & coast effectiveness.
        {
            float saving = fuel.CurrentLapSaving;
            if (Math.Abs(saving) > 0.001f)
            {
                string sign = saving >= 0 ? "-" : "+"; // negative saving = using MORE than average
                _valSavingRate.Text = sign + Math.Abs(saving).ToString("F3", CultureInfo.InvariantCulture) + " L";
                _valSavingRate.Foreground = saving > 0.005f ? BRUSH_GREEN   // saving fuel
                    : saving < -0.005f ? BRUSH_RED    // using more than average
                    : BRUSH_MUTED;                     // basically on-pace
            }
            else if (fuel.AvgFuelPerLap_L3 > 0)
            {
                _valSavingRate.Text = "0.000";
                _valSavingRate.Foreground = BRUSH_MUTED; // exactly on-pace
            }
            else
            {
                _valSavingRate.Text = "--";
                _valSavingRate.Foreground = BRUSH_MUTED;
            }
        }

        // Strategy: pit vs save
        if (fuel.NeedsFuelSaving)
        {
            if (!fuel.CanSaveFuelToFinish)
            {
                _valStrategy.Text = "PIT";
                _valStrategy.FontWeight = FontWeights.Bold;
                _valStrategy.Foreground = BRUSH_RED;
            }
            else if (fuel.IsPittingFaster)
            {
                float delta = fuel.StrategyTimeDelta;
                _valStrategy.Text = $"PIT +{delta:F1}s";
                _valStrategy.FontWeight = FontWeights.SemiBold;
                _valStrategy.Foreground = BRUSH_ORANGE;
            }
            else
            {
                _valStrategy.Text = "SAVE";
                _valStrategy.FontWeight = FontWeights.SemiBold;
                _valStrategy.Foreground = BRUSH_GREEN;
            }
        }
        else
        {
            _valStrategy.Text = fuel.HasSufficientData ? "CLEAR" : "--";
            _valStrategy.FontWeight = FontWeights.Normal;
            _valStrategy.Foreground = fuel.HasSufficientData ? BRUSH_GREEN : BRUSH_MUTED;
        }

        // ── Alert bar ───────────────────────────────────────────
        string? alert = fuel.StrategicAlert;
        if (!string.IsNullOrEmpty(alert))
        {
            _alertText.Text = alert;
            _alertBorder.Background = fuel.AlertSeverity switch
            {
                3 => new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)),
                2 => new SolidColorBrush(Color.FromArgb(60, 255, 128, 0)),
                1 => new SolidColorBrush(Color.FromArgb(40, 0, 128, 128)),
                _ => Brushes.Transparent
            };
            _alertText.Foreground = fuel.AlertSeverity switch
            {
                3 => BRUSH_RED,
                2 => BRUSH_ORANGE,
                _ => BRUSH_TEAL
            };
        }
        else
        {
            _alertText.Text = "";
            _alertBorder.Background = Brushes.Transparent;
        }
    }

    #endregion

    /// <summary>
    /// Override SetSize — this widget is not square.
    /// Scale width proportionally, auto-adjust height.
    /// </summary>
    public override void SetSize(double size)
    {
        double scale = size / WIDGET_WIDTH;
        Width = size;
        Height = _backgroundBorder.Height * scale;
        Config.Width = Width;
        Config.Height = Height;
    }
}
