using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.StandingsWidget;

/// <summary>
/// Full-field standings overlay — Relative-style table sorted by race position.
/// All columns are user-toggleable. MRT dark theme, high-contrast text, class stripes.
/// </summary>
public class StandingsWidget : WidgetBase
{
    #region Constants

    private const double PADDING = 8;
    private const double ROW_HEIGHT = 18;
    private const double ROW_GAP = 2;
    private const double HEADER_HEIGHT = 18;
    private const double SEPARATOR_HEIGHT = 1;
    private const double BORDER_RADIUS = 6;
    private const double CLASS_STRIPE_WIDTH = 3;

    // Default column widths
    private const double COL_START = 7;
    private const double COL_W_POS = 22;
    private const double COL_W_NUM = 32;
    private const double COL_W_NAME = 100;
    private const double COL_W_INTERVAL = 48;
    private const double COL_W_GAP_LEADER = 48;
    private const double COL_W_LAST = 58;
    private const double COL_W_BEST = 58;
    private const double COL_W_LAP = 26;
    private const double COL_W_PITS = 20;
    private const double COL_W_IRATING = 36;
    private const double COL_W_LICENSE = 22;
    private const double COL_W_CAR_MODEL = 30;
    private const double COL_W_POS_DELTA = 22;
    private const double COL_W_NATIONALITY = 22;

    private const int DEFAULT_VISIBLE_ROWS = 20;
    private const int MAX_DISPLAY_ROWS = 64;
    private const double MIN_WIDGET_WIDTH = 220;

    private const double FONT_DATA = 11.5;
    private const double FONT_HEADER = 9.5;
    private const int MAX_NAME_LENGTH = 14;
    private const double LAPPED_DIM_OPACITY = 0.40;

    /// <summary>Frame counter for blink effects (~60Hz).</summary>
    private int _frameCount;
    private const int BLINK_HALF_PERIOD = 30; // frames per half-cycle at 60Hz = 0.5s on/off

    #endregion

    #region Brushes

    private static readonly Typeface TYPEFACE = new("Consolas");
    private static readonly Typeface TYPEFACE_NAME = new("Segoe UI");

    // ── Color palette (aligned with RelativeWidget) ─────────────────
    private static readonly SolidColorBrush BRUSH_BG = Freeze(new(Color.FromArgb(245, 18, 18, 18)));
    private static readonly SolidColorBrush BRUSH_ROW_ALT = Freeze(new(Color.FromArgb(12, 255, 255, 255)));
    private static readonly SolidColorBrush BRUSH_TEXT = Freeze(new(Color.FromRgb(240, 240, 240)));
    private static readonly SolidColorBrush BRUSH_MUTED = Freeze(new(Color.FromRgb(140, 140, 140)));
    private static readonly SolidColorBrush BRUSH_HEADER = Freeze(new(Color.FromRgb(140, 140, 140)));
    private static readonly SolidColorBrush BRUSH_TEAL = Freeze(new(Color.FromRgb(0, 128, 128)));
    private static readonly SolidColorBrush BRUSH_ORANGE = Freeze(new(Color.FromRgb(255, 128, 0)));
    private static readonly SolidColorBrush BRUSH_PLAYER = Freeze(new(Color.FromArgb(40, 0, 128, 128)));
    private static readonly SolidColorBrush BRUSH_PIT = Freeze(new(Color.FromRgb(255, 200, 50)));
    private static readonly SolidColorBrush BRUSH_GREEN = Freeze(new(Color.FromRgb(0, 200, 80)));
    private static readonly SolidColorBrush BRUSH_RED = Freeze(new(Color.FromRgb(220, 50, 50)));
    private static readonly SolidColorBrush BRUSH_SEPARATOR = Freeze(new(Color.FromArgb(40, 80, 80, 80)));
    private static readonly SolidColorBrush BRUSH_LAPPED = Freeze(new(Color.FromArgb(100, 255, 255, 255)));
    private static readonly SolidColorBrush BRUSH_PURPLE = Freeze(new(Color.FromRgb(180, 0, 255)));
    private static readonly SolidColorBrush BRUSH_MEATBALL = Freeze(new(Color.FromRgb(255, 100, 0)));
    private static readonly SolidColorBrush BRUSH_BLACK_FLAG = Freeze(new(Color.FromRgb(0, 0, 0)));
    private static readonly SolidColorBrush BRUSH_WHITE = Freeze(new(Colors.White));
    private static readonly SolidColorBrush BRUSH_DARK_TEXT = Freeze(new(Color.FromRgb(20, 20, 20)));
    // iRacing license colors
    private static readonly SolidColorBrush BRUSH_LIC_A = Freeze(new(Color.FromRgb(0x01, 0x53, 0xDB)));
    private static readonly SolidColorBrush BRUSH_LIC_B = Freeze(new(Color.FromRgb(0x00, 0xC7, 0x02)));
    private static readonly SolidColorBrush BRUSH_LIC_C = Freeze(new(Color.FromRgb(0xFE, 0xEC, 0x04)));
    private static readonly SolidColorBrush BRUSH_LIC_D = Freeze(new(Color.FromRgb(0xFC, 0x8A, 0x27)));
    private static readonly SolidColorBrush BRUSH_LIC_R = Freeze(new(Color.FromRgb(0xFC, 0x1A, 0x2B)));

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    // Class colors (same palette as RelativeWidget)
    private static readonly Color[] CLASS_COLORS = new[]
    {
        Color.FromRgb(0, 128, 128),   // Teal
        Color.FromRgb(255, 128, 0),   // Orange
        Color.FromRgb(130, 80, 230),  // Purple
        Color.FromRgb(0, 180, 90),    // Green
        Color.FromRgb(220, 60, 60),   // Red
        Color.FromRgb(60, 140, 220),  // Blue
    };
    private readonly Dictionary<int, SolidColorBrush> _classColorCache = new();

    #endregion

    #region Settings — user-toggleable columns

    public int MaxVisibleRows { get; set; } = DEFAULT_VISIBLE_ROWS;
    public bool ShowCarNumber { get; set; } = true;
    public bool ShowInterval { get; set; } = true;
    public bool ShowGapToLeader { get; set; } = true;
    public bool ShowLastLap { get; set; } = false;
    public bool ShowBestLap { get; set; } = false;
    public bool ShowCurrentLap { get; set; } = false;
    public bool ShowPitStopCount { get; set; } = false;
    public bool ShowIRating { get; set; } = false;
    public bool ShowLicense { get; set; } = false;
    public bool ShowCarModel { get; set; } = false;
    public bool ShowPositionChange { get; set; } = false;
    public bool ShowNationality { get; set; } = false;
    public bool ShowClassPosition { get; set; } = false;
    public bool ShowAlternateRowShading { get; set; } = true;
    public bool HighlightPlayer { get; set; } = true;
    public bool DimLappedCars { get; set; } = false;
    public bool AlwaysShowPlayer { get; set; } = true;
    public NameFormat DriverNameFormat { get; set; } = NameFormat.FirstInitialLastName;

    public enum NameFormat
    {
        FirstInitialLastName, FullName, Initials, LastName,
        ThreeLetterCode, CompactJSmith
    }

    #endregion

    #region State

    private readonly StandingsCalculator _calculator = new();
    private bool _isTimedSession;
    private int _estimatedTotalLaps;
    private Canvas _canvas = null!;
    private Border _backgroundBorder = null!;
    private Canvas _rowCanvas = null!;
    private TextBlock[] _hdrTexts = Array.Empty<TextBlock>();
    private Border _hdrSeparator = null!;
    private RowElements[] _rows = new RowElements[MAX_DISPLAY_ROWS];
    private int _visibleRowCount;

    private struct ColumnLayout
    {
        public double TotalWidth;
        public double PosX, NumX, NameX, IntX, GapX, LastX, BestX, LapX, PitsX, IRX, LicX, CarX, PosDeltaX, NatX, ClassPosX;
    }
    private ColumnLayout _layout;

    private struct RowElements
    {
        public Border RowBg;
        public Border ClassStripe;
        public TextBlock Pos, Num, Name, Int, Gap, Last, Best, Lap, Pits, IR, Lic, Car, PosDelta, Nat, ClassPos;
    }

    #endregion

    #region Constructor

    public override WidgetType WidgetType => WidgetType.Standings;

    /// <summary>Update at 4Hz (every 15th tick) — standings only change on position swaps or lap completions.</summary>
    protected override int UpdateIntervalTicks => 15;

    public StandingsWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        Title = "Standings";
        LoadSettings();
        InitializeWidget();   // Must come first — creates Canvas, headers, rows
        RecalcLayout();       // Now _hdrDefs is populated for PositionHeaders()
        RecalcHeight();
    }

    private void InitializeWidget()
    {
        _canvas = new Canvas { ClipToBounds = true };
        Content = _canvas;

        _backgroundBorder = new Border
        {
            Background = BRUSH_BG,
            CornerRadius = new CornerRadius(BORDER_RADIUS),
            ClipToBounds = true,
        };
        _canvas.Children.Add(_backgroundBorder);

        _rowCanvas = new Canvas();
        _canvas.Children.Add(_rowCanvas);

        BuildHeader();
        PreAllocateRows();
    }

    #endregion

    #region Header + Row Allocation

    private readonly List<(TextBlock tb, Func<ColumnLayout, double> getX, string text)> _hdrDefs = new();

    private void BuildHeader()
    {
        _hdrDefs.Clear();
        _hdrDefs.Add((MakeHdr("P"), l => l.PosX, "P"));
        _hdrDefs.Add((MakeHdr("#"), l => l.NumX, "#"));
        _hdrDefs.Add((MakeHdr("DRIVER"), l => l.NameX, "DRIVER"));
        _hdrDefs.Add((MakeHdr("INT"), l => l.IntX, "INT"));
        _hdrDefs.Add((MakeHdr("GAP"), l => l.GapX, "GAP"));
        _hdrDefs.Add((MakeHdr("LAST"), l => l.LastX, "LAST"));
        _hdrDefs.Add((MakeHdr("BEST"), l => l.BestX, "BEST"));
        _hdrDefs.Add((MakeHdr("LAP"), l => l.LapX, "LAP"));
        _hdrDefs.Add((MakeHdr("Pt"), l => l.PitsX, "Pt"));
        _hdrDefs.Add((MakeHdr("iR"), l => l.IRX, "iR"));
        _hdrDefs.Add((MakeHdr("L"), l => l.LicX, "L"));
        _hdrDefs.Add((MakeHdr("CAR"), l => l.CarX, "CAR"));
        _hdrDefs.Add((MakeHdr("Δ"), l => l.PosDeltaX, "Δ"));
        _hdrDefs.Add((MakeHdr("NT"), l => l.NatX, "NT"));
        _hdrDefs.Add((MakeHdr("CP"), l => l.ClassPosX, "CP"));

        _hdrSeparator = new Border
        {
            Background = BRUSH_SEPARATOR,
            Height = SEPARATOR_HEIGHT,
        };
        _rowCanvas.Children.Add(_hdrSeparator);
    }

    private TextBlock MakeHdr(string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = FONT_HEADER,
            Foreground = BRUSH_HEADER,
            FontFamily = new FontFamily("Consolas"),
        };
        _rowCanvas.Children.Add(tb);
        return tb;
    }

    private void PreAllocateRows()
    {
        for (int i = 0; i < MAX_DISPLAY_ROWS; i++)
        {
            var row = new RowElements
            {
                RowBg = new Border { Height = ROW_HEIGHT, CornerRadius = new CornerRadius(2) },
                ClassStripe = new Border { Width = CLASS_STRIPE_WIDTH, Height = ROW_HEIGHT, CornerRadius = new CornerRadius(1) },
                Pos = MakeCell(FONT_DATA, BRUSH_MUTED),
                Num = MakeCell(FONT_DATA, BRUSH_MUTED),
                Name = MakeCell(FONT_DATA, BRUSH_TEXT, "Segoe UI"),
                Int = MakeCell(FONT_DATA, BRUSH_MUTED),
                Gap = MakeCell(FONT_DATA, BRUSH_MUTED),
                Last = MakeCell(FONT_DATA, BRUSH_MUTED),
                Best = MakeCell(FONT_DATA, BRUSH_MUTED),
                Lap = MakeCell(FONT_DATA, BRUSH_MUTED),
                Pits = MakeCell(FONT_DATA, BRUSH_MUTED),
                IR = MakeCell(FONT_DATA, BRUSH_MUTED),
                Lic = MakeCell(FONT_DATA, BRUSH_MUTED),
                Car = MakeCell(FONT_DATA, BRUSH_MUTED),
                PosDelta = MakeCell(FONT_DATA, BRUSH_MUTED),
                Nat = MakeCell(FONT_DATA, BRUSH_MUTED),
                ClassPos = MakeCell(FONT_DATA, BRUSH_MUTED),
            };

            _rowCanvas.Children.Add(row.RowBg);
            _rowCanvas.Children.Add(row.ClassStripe);
            AddAllCells(row);
            _rows[i] = row;
            SetRowVisible(i, false);
        }
    }

    private void AddAllCells(RowElements row)
    {
        foreach (var tb in AllCells(row))
            _rowCanvas.Children.Add(tb);
    }

    private static IEnumerable<TextBlock> AllCells(RowElements row)
    {
        yield return row.Pos; yield return row.Num; yield return row.Name;
        yield return row.Int; yield return row.Gap; yield return row.Last;
        yield return row.Best; yield return row.Lap; yield return row.Pits;
        yield return row.IR; yield return row.Lic; yield return row.Car;
        yield return row.PosDelta; yield return row.Nat; yield return row.ClassPos;
    }

    private static TextBlock MakeCell(double fontSize, SolidColorBrush fg, string font = "Consolas")
    {
        return new TextBlock
        {
            FontSize = fontSize,
            Foreground = fg,
            FontFamily = new FontFamily(font),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    #endregion

    #region Layout Calculation

    public void RecalcLayout()
    {
        double x = COL_START;
        _layout.PosX = x; x += COL_W_POS;
        _layout.ClassPosX = x; if (ShowClassPosition) x += COL_W_POS;
        _layout.NumX = x; if (ShowCarNumber) x += COL_W_NUM;
        _layout.NameX = x; x += COL_W_NAME;
        _layout.PosDeltaX = x; if (ShowPositionChange) x += COL_W_POS_DELTA;
        _layout.IntX = x; if (ShowInterval) x += COL_W_INTERVAL;
        _layout.GapX = x; if (ShowGapToLeader) x += COL_W_GAP_LEADER;
        _layout.LastX = x; if (ShowLastLap) x += COL_W_LAST;
        _layout.BestX = x; if (ShowBestLap) x += COL_W_BEST;
        _layout.LapX = x; if (ShowCurrentLap) x += COL_W_LAP;
        _layout.PitsX = x; if (ShowPitStopCount) x += COL_W_PITS;
        _layout.IRX = x; if (ShowIRating) x += COL_W_IRATING;
        _layout.LicX = x; if (ShowLicense) x += COL_W_LICENSE;
        _layout.CarX = x; if (ShowCarModel) x += COL_W_CAR_MODEL;
        _layout.NatX = x; if (ShowNationality) x += COL_W_NATIONALITY;
        x += PADDING;

        _layout.TotalWidth = Math.Max(x, MIN_WIDGET_WIDTH);
        PositionHeaders();

        Width = _layout.TotalWidth;
    }

    public void RecalcHeight(int? actualRowCount = null)
    {
        int rows = actualRowCount ?? Math.Min(MaxVisibleRows, MAX_DISPLAY_ROWS);
        rows = Math.Clamp(rows, 1, Math.Min(MaxVisibleRows, MAX_DISPLAY_ROWS));
        double h = PADDING + HEADER_HEIGHT + SEPARATOR_HEIGHT + (rows * (ROW_HEIGHT + ROW_GAP)) + PADDING;
        Height = h;

        if (_backgroundBorder != null)
        {
            _backgroundBorder.Width = _layout.TotalWidth;
            _backgroundBorder.Height = h;
        }
        if (_rowCanvas != null)
        {
            _rowCanvas.Width = _layout.TotalWidth;
            _rowCanvas.Height = h;
        }
    }

    private void PositionHeaders()
    {
        double y = PADDING;
        foreach (var (tb, getX, _) in _hdrDefs)
        {
            Canvas.SetLeft(tb, getX(_layout));
            Canvas.SetTop(tb, y);
        }
        // Show/hide headers based on toggles
        _hdrDefs[1].tb.Visibility = ShowCarNumber ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[3].tb.Visibility = ShowInterval ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[4].tb.Visibility = ShowGapToLeader ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[5].tb.Visibility = ShowLastLap ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[6].tb.Visibility = ShowBestLap ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[7].tb.Visibility = ShowCurrentLap ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[8].tb.Visibility = ShowPitStopCount ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[9].tb.Visibility = ShowIRating ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[10].tb.Visibility = ShowLicense ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[11].tb.Visibility = ShowCarModel ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[12].tb.Visibility = ShowPositionChange ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[13].tb.Visibility = ShowNationality ? Visibility.Visible : Visibility.Collapsed;
        _hdrDefs[14].tb.Visibility = ShowClassPosition ? Visibility.Visible : Visibility.Collapsed;

        Canvas.SetLeft(_hdrSeparator, COL_START);
        Canvas.SetTop(_hdrSeparator, y + HEADER_HEIGHT);
        _hdrSeparator.Width = _layout.TotalWidth - COL_START * 2;
    }

    #endregion

    #region UpdateUI

    protected override void UpdateUI(TelemetryData data)
    {
        _frameCount++;
        bool isBlinkOn = (_frameCount / BLINK_HALF_PERIOD) % 2 == 0;

        var entries = _calculator.Calculate(data, MaxVisibleRows, AlwaysShowPlayer);
        int count = Math.Min(entries.Count, MAX_DISPLAY_ROWS);

        // Detect timed session and estimated total laps for the lap column
        _isTimedSession = (data.SessionLapsTotal <= 0 || data.SessionLapsTotal > 500)
                          && data.SessionTimeRemain > 0;
        _estimatedTotalLaps = data.EstimatedTotalRaceLaps;

        double yStart = PADDING + HEADER_HEIGHT + SEPARATOR_HEIGHT + 2;

        for (int i = 0; i < count; i++)
        {
            var e = entries[i];
            var row = _rows[i];
            double y = yStart + i * (ROW_HEIGHT + ROW_GAP);

            SetRowVisible(i, true);

            // Row background
            row.RowBg.Width = _layout.TotalWidth - COL_START * 2;
            Canvas.SetLeft(row.RowBg, COL_START);
            Canvas.SetTop(row.RowBg, y);
            row.RowBg.Background = e.IsPlayer && HighlightPlayer
                ? BRUSH_PLAYER
                : (ShowAlternateRowShading && i % 2 == 1 ? BRUSH_ROW_ALT : Brushes.Transparent);

            // Class stripe
            var classBrush = GetClassBrush(e.CarClassId);
            row.ClassStripe.Background = classBrush;
            Canvas.SetLeft(row.ClassStripe, 1);
            Canvas.SetTop(row.ClassStripe, y);

            // Position
            row.Pos.Text = e.OverallPosition.ToString();
            row.Pos.Foreground = e.IsPlayer ? BRUSH_TEAL : BRUSH_TEXT;
            row.Pos.FontWeight = e.IsPlayer ? FontWeights.Bold : FontWeights.Normal;
            Canvas.SetLeft(row.Pos, _layout.PosX);
            Canvas.SetTop(row.Pos, y);

            // Class position
            row.ClassPos.Text = ShowClassPosition ? e.ClassPosition.ToString() : "";
            row.ClassPos.Visibility = ShowClassPosition ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(row.ClassPos, _layout.ClassPosX);
            Canvas.SetTop(row.ClassPos, y);

            // Car number
            row.Num.Text = ShowCarNumber ? $"#{e.CarNumber}" : "";
            row.Num.Foreground = e.IsPlayer ? BRUSH_TEAL : BRUSH_MUTED;
            row.Num.Visibility = ShowCarNumber ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(row.Num, _layout.NumX);
            Canvas.SetTop(row.Num, y);

            // Driver name — yellow when in pit, teal for player, white otherwise
            row.Name.Text = FormatName(e.DriverName);
            if (e.IsPlayer)
                row.Name.Foreground = BRUSH_TEAL;
            else if (e.IsOnPitRoad)
                row.Name.Foreground = isBlinkOn ? BRUSH_PIT : BRUSH_MUTED;
            else
                row.Name.Foreground = BRUSH_TEXT;
            row.Name.FontWeight = e.IsPlayer ? FontWeights.SemiBold : FontWeights.Normal;
            Canvas.SetLeft(row.Name, _layout.NameX);
            Canvas.SetTop(row.Name, y);

            // Position change
            row.PosDelta.Visibility = ShowPositionChange ? Visibility.Visible : Visibility.Collapsed;
            if (ShowPositionChange)
            {
                int delta = e.PositionChange;
                row.PosDelta.Text = delta > 0 ? $"▲{delta}" : delta < 0 ? $"▼{Math.Abs(delta)}" : "–";
                row.PosDelta.Foreground = delta > 0 ? BRUSH_GREEN : delta < 0 ? BRUSH_RED : BRUSH_MUTED;
            }
            Canvas.SetLeft(row.PosDelta, _layout.PosDeltaX);
            Canvas.SetTop(row.PosDelta, y);

            // Interval — lapped cars shown in red
            row.Int.Visibility = ShowInterval ? Visibility.Visible : Visibility.Collapsed;
            if (ShowInterval)
            {
                if (e.OverallPosition == 1)
                    row.Int.Text = "—";
                else if (e.LapDelta < 0)
                {
                    row.Int.Text = $"+{Math.Abs(e.LapDelta)}L";
                    row.Int.Foreground = BRUSH_RED;
                }
                else
                {
                    row.Int.Text = e.Interval > 0 ? $"+{e.Interval.ToString("F1", CultureInfo.InvariantCulture)}" : "—";
                    row.Int.Foreground = BRUSH_MUTED;
                }
            }
            Canvas.SetLeft(row.Int, _layout.IntX);
            Canvas.SetTop(row.Int, y);

            // Gap to leader — lapped cars shown in red
            row.Gap.Visibility = ShowGapToLeader ? Visibility.Visible : Visibility.Collapsed;
            if (ShowGapToLeader)
            {
                if (e.OverallPosition == 1)
                {
                    row.Gap.Text = "—";
                    row.Gap.Foreground = BRUSH_MUTED;
                }
                else if (e.LapDelta < 0)
                {
                    row.Gap.Text = $"+{Math.Abs(e.LapDelta)}L";
                    row.Gap.Foreground = BRUSH_RED;
                }
                else
                {
                    row.Gap.Text = e.GapToLeader > 0 ? $"+{e.GapToLeader.ToString("F1", CultureInfo.InvariantCulture)}" : "—";
                    row.Gap.Foreground = BRUSH_MUTED;
                }
            }
            Canvas.SetLeft(row.Gap, _layout.GapX);
            Canvas.SetTop(row.Gap, y);

            // Last lap
            row.Last.Visibility = ShowLastLap ? Visibility.Visible : Visibility.Collapsed;
            row.Last.Text = ShowLastLap && e.LastLapTime > 0 ? FormatLapTime(e.LastLapTime) : "";
            Canvas.SetLeft(row.Last, _layout.LastX);
            Canvas.SetTop(row.Last, y);

            // Best lap
            row.Best.Visibility = ShowBestLap ? Visibility.Visible : Visibility.Collapsed;
            row.Best.Text = ShowBestLap && e.BestLapTime > 0 ? FormatLapTime(e.BestLapTime) : "";
            Canvas.SetLeft(row.Best, _layout.BestX);
            Canvas.SetTop(row.Best, y);

            // Current lap — right-aligned for clean column look
            // Always show actual laps driven (CurrentLap from CarIdxLap), never estimated.
            row.Lap.Visibility = ShowCurrentLap ? Visibility.Visible : Visibility.Collapsed;
            if (ShowCurrentLap)
            {
                row.Lap.Text = e.CurrentLap.ToString();
            }
            else
            {
                row.Lap.Text = "";
            }
            row.Lap.TextAlignment = TextAlignment.Right;
            row.Lap.Width = COL_W_LAP - 4; // fit within column, slight padding
            Canvas.SetLeft(row.Lap, _layout.LapX);
            Canvas.SetTop(row.Lap, y);

            // Pit stops
            row.Pits.Visibility = ShowPitStopCount ? Visibility.Visible : Visibility.Collapsed;
            row.Pits.Text = ShowPitStopCount && e.PitStopCount > 0 ? $"{e.PitStopCount}p" : "";
            Canvas.SetLeft(row.Pits, _layout.PitsX);
            Canvas.SetTop(row.Pits, y);

            // iRating
            row.IR.Visibility = ShowIRating ? Visibility.Visible : Visibility.Collapsed;
            row.IR.Text = ShowIRating && e.IRating > 0 ? FormatIRating(e.IRating) : "";
            Canvas.SetLeft(row.IR, _layout.IRX);
            Canvas.SetTop(row.IR, y);

            // License — colored by license class (A=blue, B=green, C=yellow, D=orange, R=red)
            row.Lic.Visibility = ShowLicense ? Visibility.Visible : Visibility.Collapsed;
            if (ShowLicense)
            {
                row.Lic.Text = e.LicenseClass;
                row.Lic.Foreground = GetLicenseBrush(e.LicenseClass);
            }
            Canvas.SetLeft(row.Lic, _layout.LicX);
            Canvas.SetTop(row.Lic, y);

            // Car model
            row.Car.Visibility = ShowCarModel ? Visibility.Visible : Visibility.Collapsed;
            row.Car.Text = ShowCarModel ? TruncateModel(e.CarModel) : "";
            Canvas.SetLeft(row.Car, _layout.CarX);
            Canvas.SetTop(row.Car, y);

            // Nationality
            row.Nat.Visibility = ShowNationality ? Visibility.Visible : Visibility.Collapsed;
            row.Nat.Text = ShowNationality ? e.CountryCode : "";
            Canvas.SetLeft(row.Nat, _layout.NatX);
            Canvas.SetTop(row.Nat, y);

            // Dim lapped cars
            double rowOpacity = (DimLappedCars && e.LapDelta < 0 && !e.IsPlayer) ? LAPPED_DIM_OPACITY : 1.0;
            foreach (var tb in AllCells(_rows[i]))
                tb.Opacity = rowOpacity;
        }

        // Hide unused rows
        for (int i = count; i < _visibleRowCount; i++)
            SetRowVisible(i, false);

        _visibleRowCount = count;

        // Auto-resize height to match actual driver count (capped at MaxVisibleRows)
        RecalcHeight(count);
    }

    #endregion

    #region Formatting Helpers

    private string FormatName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "???";
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string result = DriverNameFormat switch
        {
            NameFormat.FullName => raw,
            NameFormat.LastName => parts.Length > 1 ? parts[^1] : raw,
            NameFormat.Initials => string.Join(".", parts.Select(p => p[0])) + ".",
            NameFormat.ThreeLetterCode => (parts.Length > 1 ? parts[^1] : raw).ToUpperInvariant()[..Math.Min(3, raw.Length)],
            NameFormat.CompactJSmith => parts.Length > 1
                ? $"{parts[0][0]}{parts[^1][..Math.Min(5, parts[^1].Length)]}"
                : raw,
            _ => parts.Length > 1
                ? $"{parts[0][0]}.{parts[^1][..Math.Min(MAX_NAME_LENGTH - 2, parts[^1].Length)]}"
                : raw[..Math.Min(MAX_NAME_LENGTH, raw.Length)],
        };
        return result.Length > MAX_NAME_LENGTH ? result[..MAX_NAME_LENGTH] : result;
    }

    private static string FormatLapTime(float seconds)
    {
        if (seconds <= 0) return "—";
        int mins = (int)(seconds / 60);
        float secs = seconds - mins * 60;
        return mins > 0
            ? $"{mins}:{secs.ToString("00.0", CultureInfo.InvariantCulture)}"
            : secs.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private static string FormatIRating(int ir) => ir >= 10000 ? $"{ir / 1000}k" : ir >= 1000 ? $"{(ir / 1000.0).ToString("F1", CultureInfo.InvariantCulture)}k" : ir.ToString();

    private static string TruncateModel(string model) =>
        string.IsNullOrEmpty(model) ? "" : model.Length > 3 ? model[..3] : model;

    private SolidColorBrush GetClassBrush(int classId)
    {
        if (_classColorCache.TryGetValue(classId, out var cached)) return cached;
        int idx = _classColorCache.Count % CLASS_COLORS.Length;
        var brush = Freeze(new SolidColorBrush(CLASS_COLORS[idx]));
        _classColorCache[classId] = brush;
        return brush;
    }

    private static SolidColorBrush GetLicenseBrush(string licenseClass)
    {
        if (string.IsNullOrEmpty(licenseClass)) return BRUSH_MUTED;
        return licenseClass.ToUpperInvariant() switch
        {
            "A" or "PRO" or "WC" => BRUSH_LIC_A,
            "B" => BRUSH_LIC_B,
            "C" => BRUSH_LIC_C,
            "D" => BRUSH_LIC_D,
            "R" => BRUSH_LIC_R,
            _ => BRUSH_MUTED,
        };
    }

    #endregion

    #region Row Visibility + Settings Persistence

    private void SetRowVisible(int idx, bool visible)
    {
        var v = visible ? Visibility.Visible : Visibility.Collapsed;
        _rows[idx].RowBg.Visibility = v;
        _rows[idx].ClassStripe.Visibility = v;
        foreach (var tb in AllCells(_rows[idx]))
            tb.Visibility = v;
        // When showing, restore per-column visibility (hides disabled columns)
        if (visible) RestoreColumnVisibility(idx);
    }

    private void RestoreColumnVisibility(int idx)
    {
        var row = _rows[idx];
        // Always-visible columns
        row.Pos.Visibility = Visibility.Visible;
        row.Name.Visibility = Visibility.Visible;
        // Toggleable columns
        row.Num.Visibility = ShowCarNumber ? Visibility.Visible : Visibility.Collapsed;
        row.Int.Visibility = ShowInterval ? Visibility.Visible : Visibility.Collapsed;
        row.Gap.Visibility = ShowGapToLeader ? Visibility.Visible : Visibility.Collapsed;
        row.Last.Visibility = ShowLastLap ? Visibility.Visible : Visibility.Collapsed;
        row.Best.Visibility = ShowBestLap ? Visibility.Visible : Visibility.Collapsed;
        row.Lap.Visibility = ShowCurrentLap ? Visibility.Visible : Visibility.Collapsed;
        row.Pits.Visibility = ShowPitStopCount ? Visibility.Visible : Visibility.Collapsed;
        row.IR.Visibility = ShowIRating ? Visibility.Visible : Visibility.Collapsed;
        row.Lic.Visibility = ShowLicense ? Visibility.Visible : Visibility.Collapsed;
        row.Car.Visibility = ShowCarModel ? Visibility.Visible : Visibility.Collapsed;
        row.PosDelta.Visibility = ShowPositionChange ? Visibility.Visible : Visibility.Collapsed;
        row.Nat.Visibility = ShowNationality ? Visibility.Visible : Visibility.Collapsed;
        row.ClassPos.Visibility = ShowClassPosition ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void SaveWidgetSettings() => SaveSettings();

    public void SaveSettings()
    {
        Config.Settings ??= new Dictionary<string, object>();
        Config.Settings["maxRows"] = MaxVisibleRows;
        Config.Settings["showCarNumber"] = ShowCarNumber;
        Config.Settings["showInterval"] = ShowInterval;
        Config.Settings["showGapToLeader"] = ShowGapToLeader;
        Config.Settings["showLastLap"] = ShowLastLap;
        Config.Settings["showBestLap"] = ShowBestLap;
        Config.Settings["showCurrentLap"] = ShowCurrentLap;
        Config.Settings["showPitStopCount"] = ShowPitStopCount;
        Config.Settings["showIRating"] = ShowIRating;
        Config.Settings["showLicense"] = ShowLicense;
        Config.Settings["showCarModel"] = ShowCarModel;
        Config.Settings["showPositionChange"] = ShowPositionChange;
        Config.Settings["showNationality"] = ShowNationality;
        Config.Settings["showClassPosition"] = ShowClassPosition;
        Config.Settings["showAltRows"] = ShowAlternateRowShading;
        Config.Settings["highlightPlayer"] = HighlightPlayer;
        Config.Settings["dimLapped"] = DimLappedCars;
        Config.Settings["alwaysShowPlayer"] = AlwaysShowPlayer;
        Config.Settings["nameFormat"] = (int)DriverNameFormat;
    }

    private void LoadSettings()
    {
        if (Config?.Settings == null) return;
        if (TryGetInt("maxRows", out var mr)) MaxVisibleRows = Math.Clamp(mr, 5, MAX_DISPLAY_ROWS);
        if (TryGetBool("showCarNumber", out var cn)) ShowCarNumber = cn;
        if (TryGetBool("showInterval", out var iv)) ShowInterval = iv;
        if (TryGetBool("showGapToLeader", out var gl)) ShowGapToLeader = gl;
        if (TryGetBool("showLastLap", out var ll)) ShowLastLap = ll;
        if (TryGetBool("showBestLap", out var bl)) ShowBestLap = bl;
        if (TryGetBool("showCurrentLap", out var cl)) ShowCurrentLap = cl;
        if (TryGetBool("showPitStopCount", out var ps)) ShowPitStopCount = ps;
        if (TryGetBool("showIRating", out var ir)) ShowIRating = ir;
        if (TryGetBool("showLicense", out var lc)) ShowLicense = lc;
        if (TryGetBool("showCarModel", out var cm)) ShowCarModel = cm;
        if (TryGetBool("showPositionChange", out var pc)) ShowPositionChange = pc;
        if (TryGetBool("showNationality", out var nt)) ShowNationality = nt;
        if (TryGetBool("showClassPosition", out var cp)) ShowClassPosition = cp;
        if (TryGetBool("showAltRows", out var ar)) ShowAlternateRowShading = ar;
        if (TryGetBool("highlightPlayer", out var hp)) HighlightPlayer = hp;
        if (TryGetBool("dimLapped", out var dl)) DimLappedCars = dl;
        if (TryGetBool("alwaysShowPlayer", out var asp)) AlwaysShowPlayer = asp;
        if (TryGetInt("nameFormat", out var nf) && Enum.IsDefined(typeof(NameFormat), nf))
            DriverNameFormat = (NameFormat)nf;
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

    #endregion
}
