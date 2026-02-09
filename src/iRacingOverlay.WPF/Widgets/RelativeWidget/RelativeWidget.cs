using System;
using System.Collections.Generic;
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

namespace iRacingOverlay.WPF.Widgets.RelativeWidget;

/// <summary>
/// Relative Widget — proximity-sorted competitor table overlay.
/// Shows cars around the player on track with time intervals,
/// class color stripes, pit indicators, and smart row count.
/// MRT theme: dark background, teal/orange accents, high contrast text.
/// </summary>
public class RelativeWidget : WidgetBase
{
    #region Constants

    // ── Dimensions ──────────────────────────────────────────────────
    private const double WIDGET_WIDTH = 370;
    private const double PADDING = 8;
    private const double ROW_HEIGHT = 20;
    private const double HEADER_HEIGHT = 18;
    private const double SEPARATOR_HEIGHT = 1;
    private const double BORDER_RADIUS = 6;
    private const double CLASS_STRIPE_WIDTH = 3;

    // ── Column positions (left edge, inside padding) ────────────────
    private const double COL_STRIPE = 0;
    private const double COL_POS = 7;          // Position: "3"
    private const double COL_NUMBER = 28;       // Car #: "#44"
    private const double COL_NAME = 63;         // Name: "Hamilton"
    private const double COL_INTERVAL = 170;    // Interval: "+3.2"
    private const double COL_GAP = 215;         // Gap to car ahead
    private const double COL_LASTLAP = 260;     // Last lap: "1:42.123"
    private const double COL_STATUS = 326;      // Status: "PIT" / "OFF" / "+1L"

    // ── Row limits ──────────────────────────────────────────────────
    private const int DEFAULT_AHEAD = 6;
    private const int DEFAULT_BEHIND = 6;
    private const int MAX_DISPLAY_ROWS = 15; // absolute maximum pre-allocated rows
    private const int TOTAL_SMART_ROWS = 12; // total non-player rows for smart distribution

    // ── Font sizes ──────────────────────────────────────────────────
    private const double FONT_DATA = 11.5;
    private const double FONT_HEADER = 9.5;
    private const double FONT_STATUS = 9;

    // ── Name formatting ─────────────────────────────────────────────
    private const int MAX_NAME_LENGTH = 15;

    #endregion

    #region Colors

    private static readonly Color COLOR_TEAL = Color.FromRgb(0, 128, 128);
    private static readonly Color COLOR_ORANGE = Color.FromRgb(255, 128, 0);
    private static readonly Color COLOR_DARK_BG = Color.FromArgb(245, 18, 18, 18);
    private static readonly Color COLOR_PLAYER_BG = Color.FromArgb(40, 0, 128, 128);
    private static readonly Color COLOR_TEXT = Color.FromRgb(240, 240, 240);
    private static readonly Color COLOR_MUTED = Color.FromRgb(100, 100, 100);
    private static readonly Color COLOR_DIM = Color.FromRgb(70, 70, 70);
    private static readonly Color COLOR_PIT = Color.FromRgb(255, 200, 50);

    private static readonly SolidColorBrush BRUSH_TEAL = Freeze(new SolidColorBrush(COLOR_TEAL));
    private static readonly SolidColorBrush BRUSH_ORANGE = Freeze(new SolidColorBrush(COLOR_ORANGE));
    private static readonly SolidColorBrush BRUSH_TEXT = Freeze(new SolidColorBrush(COLOR_TEXT));
    private static readonly SolidColorBrush BRUSH_MUTED = Freeze(new SolidColorBrush(COLOR_MUTED));
    private static readonly SolidColorBrush BRUSH_DIM = Freeze(new SolidColorBrush(COLOR_DIM));
    private static readonly SolidColorBrush BRUSH_PIT = Freeze(new SolidColorBrush(COLOR_PIT));
    private static readonly SolidColorBrush BRUSH_TRANSPARENT = Freeze(new SolidColorBrush(Colors.Transparent));
    private static readonly SolidColorBrush BRUSH_PLAYER_BG = Freeze(new SolidColorBrush(COLOR_PLAYER_BG));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    #endregion

    #region UI Elements

    private Canvas _canvas = null!;
    private Border _backgroundBorder = null!;
    private Canvas _rowCanvas = null!; // inner canvas holding all rows

    /// <summary>Pre-allocated row visual elements</summary>
    private readonly RowElements[] _rows = new RowElements[MAX_DISPLAY_ROWS];

    /// <summary>Current number of visible rows</summary>
    private int _visibleRowCount;

    /// <summary>Frame counter for blinking effects (toggles every ~30 frames at 60Hz = 0.5s on/off)</summary>
    private int _frameCount;
    private const int BLINK_HALF_PERIOD = 30; // frames per blink half-cycle

    #endregion

    #region Settings

    /// <summary>Use smart row count based on field position</summary>
    public bool UseSmartRowCount { get; set; } = true;

    /// <summary>Show class position instead of overall</summary>
    public bool ShowClassPosition { get; set; } = false;

    /// <summary>Show dimmed disconnected/off-track drivers</summary>
    public bool DimDisconnected { get; set; } = true;

    /// <summary>Fixed max ahead (used when smart row count is off)</summary>
    public int MaxAhead { get; set; } = DEFAULT_AHEAD;

    /// <summary>Fixed max behind (used when smart row count is off)</summary>
    public int MaxBehind { get; set; } = DEFAULT_BEHIND;

    #endregion

    #region Services

    private readonly RelativeCalculator _calculator = new();

    /// <summary>Cache of class ID → color brush (generated from hash)</summary>
    private readonly Dictionary<int, SolidColorBrush> _classColorCache = new();

    #endregion

    // ── Construction ────────────────────────────────────────────────

    public override WidgetType WidgetType => WidgetType.Relative;

    public RelativeWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        Title = "Relative";
        Width = WIDGET_WIDTH;

        LoadSettings();
        InitializeWidget();
        RecalcHeight();
    }

    // ── Settings persistence ────────────────────────────────────────

    private void LoadSettings()
    {
        if (Config?.Settings == null) return;

        if (TryGetBool("useSmartRowCount", out var smart)) UseSmartRowCount = smart;
        if (TryGetBool("showClassPosition", out var cls)) ShowClassPosition = cls;
        if (TryGetBool("dimDisconnected", out var dim)) DimDisconnected = dim;
        if (TryGetInt("maxAhead", out var ah)) MaxAhead = Math.Clamp(ah, 1, 10);
        if (TryGetInt("maxBehind", out var bh)) MaxBehind = Math.Clamp(bh, 1, 10);
    }

    public void SaveSettings()
    {
        Config.Settings ??= new Dictionary<string, object>();
        Config.Settings["useSmartRowCount"] = UseSmartRowCount;
        Config.Settings["showClassPosition"] = ShowClassPosition;
        Config.Settings["dimDisconnected"] = DimDisconnected;
        Config.Settings["maxAhead"] = MaxAhead;
        Config.Settings["maxBehind"] = MaxBehind;
    }

    private bool TryGetBool(string key, out bool value)
    {
        value = default;
        if (Config?.Settings == null || !Config.Settings.TryGetValue(key, out var raw)) return false;
        if (raw is JsonElement je) { value = je.ValueKind == JsonValueKind.True; return true; }
        if (raw is bool b) { value = b; return true; }
        return false;
    }

    private bool TryGetInt(string key, out int value)
    {
        value = default;
        if (Config?.Settings == null || !Config.Settings.TryGetValue(key, out var raw)) return false;
        if (raw is JsonElement je && je.TryGetInt32(out var i)) { value = i; return true; }
        if (raw is int n) { value = n; return true; }
        return false;
    }

    // ── Widget initialization ───────────────────────────────────────

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

        // Inner canvas for absolute positioning of rows
        _rowCanvas = new Canvas
        {
            Width = WIDGET_WIDTH - (PADDING * 2),
            ClipToBounds = true
        };
        _backgroundBorder.Child = new Border
        {
            Padding = new Thickness(PADDING),
            Child = _rowCanvas
        };

        // Header row
        CreateHeaderRow();

        // Pre-allocate data rows
        double yStart = HEADER_HEIGHT + SEPARATOR_HEIGHT + 4;
        for (int i = 0; i < MAX_DISPLAY_ROWS; i++)
        {
            _rows[i] = CreateDataRow(yStart + (i * ROW_HEIGHT));
            _rows[i].Container.Visibility = Visibility.Collapsed;
        }
    }

    private void CreateHeaderRow()
    {
        double y = 0;
        AddText(_rowCanvas, "P", COL_POS, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        AddText(_rowCanvas, "#", COL_NUMBER, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        AddText(_rowCanvas, "NAME", COL_NAME, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        AddText(_rowCanvas, "INT", COL_INTERVAL, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        AddText(_rowCanvas, "GAP", COL_GAP, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        AddText(_rowCanvas, "LAST", COL_LASTLAP, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);

        // Separator line
        var sep = new Border
        {
            Width = WIDGET_WIDTH - (PADDING * 2),
            Height = SEPARATOR_HEIGHT,
            Background = BRUSH_MUTED,
            Opacity = 0.3
        };
        Canvas.SetLeft(sep, 0);
        Canvas.SetTop(sep, HEADER_HEIGHT + 2);
        _rowCanvas.Children.Add(sep);
    }

    private RowElements CreateDataRow(double y)
    {
        var row = new RowElements();

        // Row background (used for player highlight)
        row.Background = new Border
        {
            Width = WIDGET_WIDTH - (PADDING * 2),
            Height = ROW_HEIGHT,
            Background = BRUSH_TRANSPARENT,
            CornerRadius = new CornerRadius(2)
        };
        Canvas.SetLeft(row.Background, 0);
        Canvas.SetTop(row.Background, y);
        _rowCanvas.Children.Add(row.Background);

        // Class color stripe
        row.ClassStripe = new Border
        {
            Width = CLASS_STRIPE_WIDTH,
            Height = ROW_HEIGHT - 4,
            CornerRadius = new CornerRadius(1),
            Background = BRUSH_TRANSPARENT
        };
        Canvas.SetLeft(row.ClassStripe, COL_STRIPE);
        Canvas.SetTop(row.ClassStripe, y + 2);
        _rowCanvas.Children.Add(row.ClassStripe);

        // Position
        row.Position = CreateRowText(COL_POS, y, FONT_DATA, BRUSH_TEXT, FontWeights.Normal);

        // Car number
        row.CarNumber = CreateRowText(COL_NUMBER, y, FONT_DATA, BRUSH_MUTED, FontWeights.Normal);

        // Driver name
        row.Name = CreateRowText(COL_NAME, y, FONT_DATA, BRUSH_TEXT, FontWeights.Normal);

        // Interval
        row.Interval = CreateRowText(COL_INTERVAL, y, FONT_DATA, BRUSH_TEAL, FontWeights.SemiBold);

        // Gap to car ahead
        row.Gap = CreateRowText(COL_GAP, y, FONT_DATA, BRUSH_MUTED, FontWeights.Normal);

        // Last lap time
        row.LastLap = CreateRowText(COL_LASTLAP, y, FONT_DATA, BRUSH_TEXT, FontWeights.Normal);

        // Status (PIT / +1L / -2L)
        row.Status = CreateRowText(COL_STATUS, y, FONT_STATUS, BRUSH_PIT, FontWeights.Bold);

        // Container to group visibility
        row.Container = new Canvas { Width = 0, Height = 0 };
        Canvas.SetLeft(row.Container, 0);
        Canvas.SetTop(row.Container, 0);
        _rowCanvas.Children.Add(row.Container);

        return row;
    }

    private TextBlock CreateRowText(double x, double y, double fontSize, SolidColorBrush brush, FontWeight weight)
    {
        var tb = new TextBlock
        {
            FontSize = fontSize,
            Foreground = brush,
            FontWeight = weight,
            FontFamily = new FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0, 1, 0, 0)
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        _rowCanvas.Children.Add(tb);
        return tb;
    }

    private static TextBlock AddText(Canvas parent, string text, double x, double y,
        double fontSize, SolidColorBrush brush, FontWeight weight)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = brush,
            FontWeight = weight,
            FontFamily = new FontFamily("Segoe UI")
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        parent.Children.Add(tb);
        return tb;
    }

    private void RecalcHeight()
    {
        int maxRows = MaxAhead + 1 + MaxBehind; // +1 for player
        double contentHeight = PADDING + HEADER_HEIGHT + SEPARATOR_HEIGHT + 4 + (maxRows * ROW_HEIGHT) + PADDING;
        Height = contentHeight;
        _backgroundBorder.Height = contentHeight;
    }

    // ── Core update loop ────────────────────────────────────────────

    protected override void UpdateUI(TelemetryData data)
    {
        // Determine row counts
        int maxAhead, maxBehind;
        if (UseSmartRowCount && data.LivePosition > 0)
        {
            // Estimate total cars from CarIdxPosition array
            int totalCars = CountActiveCars(data);
            (maxAhead, maxBehind) = RelativeCalculator.GetSmartRowCount(
                data.LivePosition, totalCars, TOTAL_SMART_ROWS);
        }
        else
        {
            maxAhead = MaxAhead;
            maxBehind = MaxBehind;
        }

        // Calculate relative entries
        var entries = _calculator.Calculate(data, maxAhead, maxBehind);

        // Advance blink frame counter
        _frameCount++;

        // Update display rows
        UpdateRows(entries);

        // Resize if row count changed
        int newRowCount = entries.Count;
        if (newRowCount != _visibleRowCount)
        {
            _visibleRowCount = newRowCount;
            double contentHeight = PADDING + HEADER_HEIGHT + SEPARATOR_HEIGHT + 4
                + (Math.Max(newRowCount, 1) * ROW_HEIGHT) + PADDING;
            Height = contentHeight;
            _backgroundBorder.Height = contentHeight;
        }
    }

    private void UpdateRows(List<RelativeEntry> entries)
    {
        for (int i = 0; i < MAX_DISPLAY_ROWS; i++)
        {
            var row = _rows[i];

            if (i < entries.Count)
            {
                var entry = entries[i];
                ShowRow(row, true);
                PopulateRow(row, entry);
            }
            else
            {
                ShowRow(row, false);
            }
        }
    }

    private void PopulateRow(RowElements row, RelativeEntry entry)
    {
        // Determine dimming
        bool dimmed = DimDisconnected && !entry.IsConnected && !entry.IsPlayer;
        var textBrush = dimmed ? BRUSH_DIM : BRUSH_TEXT;
        var mutedBrush = dimmed ? BRUSH_DIM : BRUSH_MUTED;

        // Class stripe
        row.ClassStripe.Background = GetClassBrush(entry.CarClassId, dimmed);

        // Player row highlight
        row.Background.Background = entry.IsPlayer ? BRUSH_PLAYER_BG : BRUSH_TRANSPARENT;

        // Position
        int pos = ShowClassPosition ? entry.ClassPosition : entry.OverallPosition;
        row.Position.Text = pos > 0 ? pos.ToString() : "-";
        row.Position.Foreground = dimmed ? BRUSH_DIM : (entry.IsPlayer ? BRUSH_TEAL : BRUSH_TEXT);

        // Car number
        row.CarNumber.Text = $"#{entry.CarNumber}";
        row.CarNumber.Foreground = mutedBrush;

        // Name — use driver name if available, otherwise car number fallback
        string name = !string.IsNullOrEmpty(entry.DriverName)
            ? FormatDriverName(entry.DriverName)
            : $"#{entry.CarNumber}";
        row.Name.Text = name;
        row.Name.Foreground = textBrush;
        row.Name.FontWeight = entry.IsPlayer ? FontWeights.Bold : FontWeights.Normal;

        // Interval — SWAPPED: behind (negative) = teal, ahead (positive) = orange
        if (entry.IsPlayer)
        {
            row.Interval.Text = "---";
            row.Interval.Foreground = BRUSH_MUTED;
        }
        else
        {
            row.Interval.Text = FormatInterval(entry.IntervalToPlayer, entry.LapDelta);
            if (dimmed)
                row.Interval.Foreground = BRUSH_DIM;
            else if (entry.IntervalToPlayer > 0)
                row.Interval.Foreground = BRUSH_ORANGE;  // ahead = orange
            else
                row.Interval.Foreground = BRUSH_TEAL;    // behind = teal
        }

        // Gap to car ahead in race position
        if (entry.IsPlayer || entry.GapToCarAhead <= 0f)
        {
            row.Gap.Text = "---";
            row.Gap.Foreground = BRUSH_MUTED;
        }
        else
        {
            row.Gap.Text = entry.GapToCarAhead < 100f
                ? string.Format(CultureInfo.InvariantCulture, "{0:F1}", entry.GapToCarAhead)
                : ">99";
            row.Gap.Foreground = mutedBrush;
        }

        // Last lap time — 3 decimals
        row.LastLap.Text = FormatLapTime(entry.LastLapTime);
        row.LastLap.Foreground = mutedBrush;

        // Status: PIT > OFF > Lap delta
        if (entry.IsOnPitRoad)
        {
            row.Status.Text = "PIT";
            row.Status.Foreground = BRUSH_PIT;
        }
        else if (entry.IsOffTrack && !entry.IsPlayer && entry.OffTrackDuration >= 0.5f)
        {
            // Flash OFF text when off-track > 2 seconds
            bool blinkVisible = entry.OffTrackDuration < 2.0f
                || (_frameCount / BLINK_HALF_PERIOD) % 2 == 0;
            row.Status.Text = blinkVisible ? "OFF" : "";
            row.Status.Foreground = BRUSH_ORANGE;
        }
        else if (entry.LapDelta != 0)
        {
            row.Status.Text = entry.LapDelta > 0 ? $"+{entry.LapDelta}L" : $"{entry.LapDelta}L";
            row.Status.Foreground = dimmed ? BRUSH_DIM
                : entry.LapDelta > 0 ? BRUSH_TEAL : BRUSH_ORANGE;
        }
        else
        {
            row.Status.Text = string.Empty;
        }
    }

    // ── Row visibility helpers ──────────────────────────────────────

    private static void ShowRow(RowElements row, bool visible)
    {
        var vis = visible ? Visibility.Visible : Visibility.Collapsed;
        row.Container.Visibility = vis;
        row.Background.Visibility = vis;
        row.ClassStripe.Visibility = vis;
        row.Position.Visibility = vis;
        row.CarNumber.Visibility = vis;
        row.Name.Visibility = vis;
        row.Interval.Visibility = vis;
        row.Gap.Visibility = vis;
        row.LastLap.Visibility = vis;
        row.Status.Visibility = vis;
    }

    // ── Formatting helpers ──────────────────────────────────────────

    /// <summary>Format driver name as "F.Lastname", truncated.</summary>
    private static string FormatDriverName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "---";

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "---";

        string formatted;
        if (parts.Length == 1)
        {
            formatted = parts[0];
        }
        else
        {
            // "Joe Smith" → "J.Smith"
            string initial = parts[0][..1].ToUpperInvariant();
            string lastName = parts[^1];
            formatted = $"{initial}.{lastName}";
        }

        return formatted.Length > MAX_NAME_LENGTH ? formatted[..MAX_NAME_LENGTH] : formatted;
    }

    /// <summary>Format lap time as "M:SS.sss" or "--:--.---" if invalid.</summary>
    private static string FormatLapTime(float seconds)
    {
        if (seconds <= 0f || seconds > 3600f) return "--:--.---";

        int min = (int)(seconds / 60f);
        float sec = seconds - (min * 60f);
        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00.000}", min, sec);
    }

    /// <summary>Format interval as "+3.2" / "-1.5" with lap delta override.</summary>
    private static string FormatInterval(float intervalSeconds, int lapDelta)
    {
        // For lapped cars, show lap indicator only
        if (Math.Abs(lapDelta) >= 1)
        {
            string lapStr = lapDelta > 0 ? $"+{lapDelta}L" : $"{lapDelta}L";
            return lapStr;
        }

        // Time interval
        string sign = intervalSeconds >= 0 ? "+" : "";
        return string.Format(CultureInfo.InvariantCulture, "{0}{1:F1}", sign, intervalSeconds);
    }

    // ── Class color generation ──────────────────────────────────────

    private SolidColorBrush GetClassBrush(int classId, bool dimmed)
    {
        if (dimmed)
            return BRUSH_DIM;

        if (_classColorCache.TryGetValue(classId, out var cached))
            return cached;

        var color = GenerateClassColor(classId);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _classColorCache[classId] = brush;
        return brush;
    }

    /// <summary>
    /// Generate a distinct, saturated color from a class ID using
    /// the golden-angle hue distribution for maximum separation.
    /// </summary>
    private static Color GenerateClassColor(int classId)
    {
        // Golden angle ≈ 137.508° for maximally separated hues
        float hue = (classId * 137.508f) % 360f;
        return HslToRgb(hue, 0.75f, 0.55f);
    }

    private static Color HslToRgb(float h, float s, float l)
    {
        float c = (1f - Math.Abs(2f * l - 1f)) * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = l - c / 2f;

        float r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    // ── Utility ─────────────────────────────────────────────────────

    /// <summary>Count active cars (valid position > 0) in the field.</summary>
    private static int CountActiveCars(TelemetryData data)
    {
        if (data.CarIdxPosition == null) return 0;
        int count = 0;
        int len = Math.Min(data.CarIdxPosition.Length, 64);
        for (int i = 0; i < len; i++)
            if (data.CarIdxPosition[i] > 0) count++;
        return count;
    }

    // ── Row element storage ─────────────────────────────────────────

    /// <summary>
    /// Holds references to all visual elements in a single row,
    /// allowing O(1) updates without creating/destroying UI objects.
    /// </summary>
    private class RowElements
    {
        public Canvas Container = null!;
        public Border Background = null!;
        public Border ClassStripe = null!;
        public TextBlock Position = null!;
        public TextBlock CarNumber = null!;
        public TextBlock Name = null!;
        public TextBlock Interval = null!;
        public TextBlock Gap = null!;
        public TextBlock LastLap = null!;
        public TextBlock Status = null!;
    }
}
