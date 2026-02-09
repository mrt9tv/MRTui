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
    private const double PADDING = 8;
    private const double ROW_HEIGHT = 18;
    private const double ROW_GAP = 1;
    private const double HEADER_HEIGHT = 18;
    private const double SEPARATOR_HEIGHT = 1;
    private const double BORDER_RADIUS = 6;
    private const double CLASS_STRIPE_WIDTH = 3;

    // ── Column widths (dynamic positioning) ─────────────────────────
    // Order: Stripe | P | [#] | NAME | [INFO] | [INT] | [LAST] | GAP | STATUS
    private const double COL_START = 7;        // gap after stripe
    private const double COL_W_POS = 21;       // "12"
    private const double COL_W_NUM = 35;       // "#44"
    private const double COL_W_NAME = 102;     // "J.Hamilton"
    private const double COL_W_INFO = 56;      // "A 3.2k" (SR class + iRating, compact)
    private const double COL_W_INFO_FULL = 68;  // "A 2035" (SR class + iRating, full)
    private const double COL_W_INT = 45;       // "+3.2" / "+1L"
    private const double COL_W_LAST = 62;      // "1:42.123"
    private const double COL_W_GAP = 62;       // "+3.2 +1L" (relative interval + lap delta)
    private const double COL_W_STATUS = 72;    // "BOX 1:23.4" (outside the box)
    private const double STATUS_GAP = 8;       // gap between box edge and status
    private const double STATUS_BG_OPACITY = 0.65; // status background ~65%
    private const double MIN_WIDGET_WIDTH = 250;
    private const double ALT_ROW_ALPHA = 12;    // alternate row shading alpha (subtle)
    private const double COL_W_CAR_MODEL = 30;  // "488" car model abbreviation
    private const double COL_W_POS_DELTA = 22;  // "▲3" position change
    private const double COL_W_NAT = 22;        // "DE" nationality code
    private const double COL_W_PITS = 20;       // "2p" pit stop count
    private const double COL_W_CLOSE = 14;      // "▲" closing rate arrow
    private const double INFO_BAR_HEIGHT = 18;  // bottom info bar height

    // ── Row limits ──────────────────────────────────────────────────
    private const int DEFAULT_AHEAD = 3;
    private const int DEFAULT_BEHIND = 3;
    private const int MAX_DISPLAY_ROWS = 15; // absolute maximum pre-allocated rows
    private const int TOTAL_SMART_ROWS = 6; // total non-player rows for smart distribution

    // ── Font sizes ──────────────────────────────────────────────────
    private const double FONT_DATA = 11.5;
    private const double FONT_HEADER = 9.5;
    private const double FONT_STATUS = 9;

    // ── Name formatting ─────────────────────────────────────────────
    private const int MAX_NAME_LENGTH = 15;

    /// <summary>How to display driver names in the Relative widget.</summary>
    public enum NameFormat
    {
        FirstInitialLastName,   // "J.Smith" (default)
        FullName,               // "Joe Smith"
        InitialsOnly,           // "J.S."
        FirstThreeLetters,      // "JOS" / "SMI"
        LastNameOnly,           // "Smith"
        FirstNameOnly,          // "Joe"
        ThreeLetterCode,        // "SMI" (first 3 of last name, uppercased)
        LastCommaFirst,         // "Smith, J."
        FirstLast4,             // "J.Smit" (first initial + last 4 chars of last name)
        FirstNameLastInit,      // "Joe S." (first name + last initial)
        UpperLastName,          // "SMITH" (last name all caps)
        InitialLast3,           // "J.Smi" (first initial + 3 chars of last name)
        LastSpaceFirst,         // "Smith J" (last name + first initial, no comma)
        CompactNoPrefix,        // "JSmith" (compact, no dot)
    }

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
    private static readonly Color COLOR_PURPLE = Color.FromRgb(180, 0, 255);
    private static readonly Color COLOR_MEATBALL = Color.FromRgb(255, 100, 0); // orange meatball
    private static readonly Color COLOR_BLACK_FLAG = Color.FromRgb(0, 0, 0); // solid black for black flag

    private static readonly Color COLOR_SEPARATOR = Color.FromArgb(40, 80, 80, 80);

    private static readonly SolidColorBrush BRUSH_TEAL = Freeze(new SolidColorBrush(COLOR_TEAL));
    private static readonly SolidColorBrush BRUSH_ORANGE = Freeze(new SolidColorBrush(COLOR_ORANGE));
    private static readonly SolidColorBrush BRUSH_TEXT = Freeze(new SolidColorBrush(COLOR_TEXT));
    private static readonly SolidColorBrush BRUSH_MUTED = Freeze(new SolidColorBrush(COLOR_MUTED));
    private static readonly SolidColorBrush BRUSH_DIM = Freeze(new SolidColorBrush(COLOR_DIM));
    private static readonly SolidColorBrush BRUSH_PIT = Freeze(new SolidColorBrush(COLOR_PIT));
    private static readonly SolidColorBrush BRUSH_TRANSPARENT = Freeze(new SolidColorBrush(Colors.Transparent));
    private static readonly SolidColorBrush BRUSH_PLAYER_BG = Freeze(new SolidColorBrush(COLOR_PLAYER_BG));
    private static readonly SolidColorBrush BRUSH_PURPLE = Freeze(new SolidColorBrush(COLOR_PURPLE));
    private static readonly SolidColorBrush BRUSH_MEATBALL = Freeze(new SolidColorBrush(COLOR_MEATBALL));
    private static readonly SolidColorBrush BRUSH_BLACK_FLAG = Freeze(new SolidColorBrush(COLOR_BLACK_FLAG));
    private static readonly SolidColorBrush BRUSH_WHITE = Freeze(new SolidColorBrush(Colors.White));
    private static readonly SolidColorBrush BRUSH_DARK_TEXT = Freeze(new SolidColorBrush(Color.FromRgb(20, 20, 20)));
    private static readonly SolidColorBrush BRUSH_STATUS_BG_DEFAULT = Freeze(new SolidColorBrush(Color.FromArgb(170, 18, 18, 18)));
    private static readonly SolidColorBrush BRUSH_STATUS_BG_BLACK = Freeze(new SolidColorBrush(Color.FromArgb(240, 0, 0, 0)));
    private static readonly SolidColorBrush BRUSH_GREEN = Freeze(new SolidColorBrush(Color.FromRgb(0, 200, 80)));
    private static readonly SolidColorBrush BRUSH_RED = Freeze(new SolidColorBrush(Color.FromRgb(220, 50, 50)));

    // ── iRacing License Colors ──────────────────────────────────────
    private static readonly SolidColorBrush BRUSH_LIC_A = Freeze(new SolidColorBrush(Color.FromRgb(0x01, 0x53, 0xDB))); // Blue
    private static readonly SolidColorBrush BRUSH_LIC_B = Freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xC7, 0x02))); // Green
    private static readonly SolidColorBrush BRUSH_LIC_C = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xEC, 0x04))); // Yellow
    private static readonly SolidColorBrush BRUSH_LIC_D = Freeze(new SolidColorBrush(Color.FromRgb(0xFC, 0x8A, 0x27))); // Orange
    private static readonly SolidColorBrush BRUSH_LIC_R = Freeze(new SolidColorBrush(Color.FromRgb(0xFC, 0x1A, 0x2B))); // Red

    private static SolidColorBrush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    #endregion

    #region Layout State

    /// <summary>Current column X positions and widget width, recomputed when toggles change.</summary>
    private struct ColumnLayout
    {
        public double BoxWidth;     // width of the main bordered box (without status)
        public double TotalWidth;   // total widget including status overhang
        public double PosX, PosDeltaX, NumX, CarModelX, NatX, NameX, InfoX, PitsX, IntX, LastX, CloseX, GapX, StatusX;
    }

    private ColumnLayout _layout;
    private bool _layoutDirty = true;

    #endregion

    #region UI Elements

    private Canvas _canvas = null!;
    private Border _backgroundBorder = null!;
    private Canvas _rowCanvas = null!;

    // Header TextBlocks (stored for repositioning)
    private TextBlock _hdrP = null!, _hdrNum = null!, _hdrCar = null!, _hdrName = null!;
    private TextBlock _hdrInfo = null!, _hdrInt = null!, _hdrLast = null!;
    private TextBlock _hdrGap = null!;
    private TextBlock _hdrPosDelta = null!, _hdrNat = null!, _hdrPits = null!;
    private Border _hdrSeparator = null!;

    // Class legend elements (up to 6 classes)
    private const int MAX_LEGEND_ITEMS = 6;
    private readonly Border[] _legendSwatches = new Border[MAX_LEGEND_ITEMS];
    private readonly TextBlock[] _legendLabels = new TextBlock[MAX_LEGEND_ITEMS];

    // Info bar elements (bottom of widget)
    private Border _infoBarBorder = null!;
    private TextBlock _infoEstLaps = null!;
    private TextBlock _infoTimeRemain = null!;
    private TextBlock _infoIncidents = null!;

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

    /// <summary>Fixed max ahead (used when smart row count is off)</summary>
    public int MaxAhead { get; set; } = DEFAULT_AHEAD;

    /// <summary>Fixed max behind (used when smart row count is off)</summary>
    public int MaxBehind { get; set; } = DEFAULT_BEHIND;

    /// <summary>Show car number column</summary>
    public bool ShowCarNumber { get; set; } = true;

    /// <summary>Show combined driver info column (license class badge + iRating in X.Yk format)</summary>
    public bool ShowDriverInfo { get; set; } = false;

    /// <summary>Show interval-to-player column (default OFF)</summary>
    public bool ShowInterval { get; set; } = false;

    /// <summary>Show last lap time column (default OFF)</summary>
    public bool ShowLastLap { get; set; } = false;

    /// <summary>Driver name display format</summary>
    public NameFormat DriverNameFormat { get; set; } = NameFormat.FirstInitialLastName;

    /// <summary>Show car model 3-letter abbreviation column</summary>
    public bool ShowCarModel { get; set; } = false;

    /// <summary>Show alternate row shading for readability</summary>
    public bool ShowAlternateRowShading { get; set; } = false;

    /// <summary>Show info bar at the bottom (est laps, time remaining, incidents)</summary>
    public bool ShowInfoBar { get; set; } = false;

    /// <summary>Show closing rate arrow (▲ closing / ▼ pulling away)</summary>
    public bool ShowClosingRate { get; set; } = false;

    /// <summary>Show pit stop count per driver</summary>
    public bool ShowPitStopCount { get; set; } = false;

    /// <summary>Show position change arrows (▲+2 / ▼-1)</summary>
    public bool ShowPositionChange { get; set; } = false;

    /// <summary>Color REL column based on gap trend (green=closing, red=opening)</summary>
    public bool ShowSectorDelta { get; set; } = false;

    /// <summary>Show 2-letter nationality/country code</summary>
    public bool ShowNationality { get; set; } = false;

    /// <summary>Show class color legend in header</summary>
    public bool ShowClassLegend { get; set; } = false;

    /// <summary>Show full iRating (e.g. 2035) instead of compact (e.g. 2.0k)</summary>
    public bool UseFullIRating { get; set; } = false;

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
        
        LoadSettings();
        RecalcLayout();
        Width = _layout.TotalWidth;
        InitializeWidget();
        RecalcHeight();
    }

    // ── Settings persistence ────────────────────────────────────────

    private void LoadSettings()
    {
        if (Config?.Settings == null) return;

        if (TryGetBool("useSmartRowCount", out var smart)) UseSmartRowCount = smart;
        if (TryGetBool("showClassPosition", out var cls)) ShowClassPosition = cls;
        if (TryGetInt("maxAhead", out var ah)) MaxAhead = Math.Clamp(ah, 1, 10);
        if (TryGetInt("maxBehind", out var bh)) MaxBehind = Math.Clamp(bh, 1, 10);
        if (TryGetBool("showCarNumber", out var cn)) ShowCarNumber = cn;
        if (TryGetBool("showDriverInfo", out var di)) ShowDriverInfo = di;
        if (TryGetBool("showInterval", out var iv)) ShowInterval = iv;
        if (TryGetBool("showLastLap", out var ll)) ShowLastLap = ll;
        if (TryGetInt("nameFormat", out var nf) && Enum.IsDefined(typeof(NameFormat), nf))
            DriverNameFormat = (NameFormat)nf;
        if (TryGetBool("showCarModel", out var cm)) ShowCarModel = cm;
        if (TryGetBool("showAlternateRowShading", out var ars)) ShowAlternateRowShading = ars;
        if (TryGetBool("showInfoBar", out var ib)) ShowInfoBar = ib;
        if (TryGetBool("showClosingRate", out var cr)) ShowClosingRate = cr;
        if (TryGetBool("showPitStopCount", out var psc)) ShowPitStopCount = psc;
        if (TryGetBool("showPositionChange", out var pc)) ShowPositionChange = pc;
        if (TryGetBool("showSectorDelta", out var sd)) ShowSectorDelta = sd;
        if (TryGetBool("showNationality", out var nat)) ShowNationality = nat;
        if (TryGetBool("showClassLegend", out var cl)) ShowClassLegend = cl;
        if (TryGetBool("useFullIRating", out var fir)) UseFullIRating = fir;
        // Migration: old showIRating/showSafetyRating → showDriverInfo
        if (TryGetBool("showIRating", out var oldIr) && oldIr) ShowDriverInfo = true;
        if (TryGetBool("showSafetyRating", out var oldSr) && oldSr) ShowDriverInfo = true;
    }

    public void SaveSettings()
    {
        Config.Settings ??= new Dictionary<string, object>();
        Config.Settings["useSmartRowCount"] = UseSmartRowCount;
        Config.Settings["showClassPosition"] = ShowClassPosition;
        Config.Settings["maxAhead"] = MaxAhead;
        Config.Settings["maxBehind"] = MaxBehind;
        Config.Settings["showCarNumber"] = ShowCarNumber;
        Config.Settings["showDriverInfo"] = ShowDriverInfo;
        Config.Settings["showInterval"] = ShowInterval;
        Config.Settings["showLastLap"] = ShowLastLap;
        Config.Settings["nameFormat"] = (int)DriverNameFormat;
        Config.Settings["showCarModel"] = ShowCarModel;
        Config.Settings["showAlternateRowShading"] = ShowAlternateRowShading;
        Config.Settings["showInfoBar"] = ShowInfoBar;
        Config.Settings["showClosingRate"] = ShowClosingRate;
        Config.Settings["showPitStopCount"] = ShowPitStopCount;
        Config.Settings["showPositionChange"] = ShowPositionChange;
        Config.Settings["showSectorDelta"] = ShowSectorDelta;
        Config.Settings["showNationality"] = ShowNationality;
        Config.Settings["showClassLegend"] = ShowClassLegend;
        Config.Settings["useFullIRating"] = UseFullIRating;
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

    // ── Dynamic layout computation ──────────────────────────────────

    /// <summary>
    /// Recompute column positions and widget width based on current toggle state.
    /// Call this when any column visibility toggle changes.
    /// </summary>
    public void RecalcLayout()
    {
        double x = COL_START;

        _layout.PosX = x;
        x += COL_W_POS;

        _layout.PosDeltaX = x;
        if (ShowPositionChange) x += COL_W_POS_DELTA;

        _layout.NumX = x;
        if (ShowCarNumber) x += COL_W_NUM;

        _layout.CarModelX = x;
        if (ShowCarModel) x += COL_W_CAR_MODEL;

        _layout.NatX = x;
        if (ShowNationality) x += COL_W_NAT;

        _layout.NameX = x;
        x += COL_W_NAME;

        _layout.InfoX = x;
        if (ShowDriverInfo) x += UseFullIRating ? COL_W_INFO_FULL : COL_W_INFO;

        _layout.PitsX = x;
        if (ShowPitStopCount) x += COL_W_PITS;

        _layout.IntX = x;
        if (ShowInterval) x += COL_W_INT;

        _layout.LastX = x;
        if (ShowLastLap) x += COL_W_LAST;

        _layout.CloseX = x;
        if (ShowClosingRate) x += COL_W_CLOSE;

        _layout.GapX = x;
        x += COL_W_GAP;

        // Box ends here — status floats OUTSIDE
        _layout.BoxWidth = Math.Max(x + PADDING * 2, MIN_WIDGET_WIDTH);
        _layout.StatusX = _layout.BoxWidth + STATUS_GAP; // to the right of the box border
        _layout.TotalWidth = _layout.StatusX + COL_W_STATUS;
        _layoutDirty = true;
    }

    /// <summary>Apply the current layout to widget dimensions and header elements.</summary>
    private void ApplyLayout()
    {
        double w = _layout.TotalWidth;
        double boxW = _layout.BoxWidth;
        Width = w;
        _canvas.Width = w;
        _backgroundBorder.Width = boxW; // box only — status floats outside
        if (_rowCanvas != null)
            _rowCanvas.Width = boxW - PADDING * 2;

        // Reposition header elements
        if (_hdrP != null)
        {
            Canvas.SetLeft(_hdrP, _layout.PosX);
            Canvas.SetLeft(_hdrNum, _layout.NumX);
            _hdrNum.Visibility = ShowCarNumber ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(_hdrCar, _layout.CarModelX);
            _hdrCar.Visibility = ShowCarModel ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(_hdrName, _layout.NameX);
            Canvas.SetLeft(_hdrInfo, _layout.InfoX);
            _hdrInfo.Visibility = ShowDriverInfo ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(_hdrInt, _layout.IntX);
            _hdrInt.Visibility = ShowInterval ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(_hdrLast, _layout.LastX);
            _hdrLast.Visibility = ShowLastLap ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(_hdrGap, _layout.GapX);
            Canvas.SetLeft(_hdrPosDelta, _layout.PosDeltaX);
            _hdrPosDelta.Visibility = ShowPositionChange ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(_hdrNat, _layout.NatX);
            _hdrNat.Visibility = ShowNationality ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(_hdrPits, _layout.PitsX);
            _hdrPits.Visibility = ShowPitStopCount ? Visibility.Visible : Visibility.Collapsed;
            _hdrSeparator.Width = boxW - PADDING * 2;
        }

        _layoutDirty = false;
    }

    // ── Widget initialization ───────────────────────────────────────

    private void InitializeWidget()
    {
        double w = _layout.TotalWidth;
        double boxW = _layout.BoxWidth;

        _canvas = new Canvas { Width = w, ClipToBounds = false };
        Content = _canvas;

        _backgroundBorder = new Border
        {
            Width = boxW,
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

        _rowCanvas = new Canvas { Width = boxW - PADDING * 2, ClipToBounds = false };
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

        // Info bar at the bottom (created once, repositioned dynamically)
        _infoBarBorder = new Border
        {
            Height = INFO_BAR_HEIGHT,
            Background = new SolidColorBrush(Color.FromArgb(80, 0, 128, 128)),
            CornerRadius = new CornerRadius(0, 0, 4, 4),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 128, 128)),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        Canvas.SetLeft(_infoBarBorder, 0);
        _rowCanvas.Children.Add(_infoBarBorder);

        _infoEstLaps = new TextBlock
        {
            FontSize = 9.5,
            Foreground = BRUSH_TEXT,
            FontFamily = new FontFamily("Segoe UI"),
            Padding = new Thickness(4, 2, 0, 0)
        };
        _rowCanvas.Children.Add(_infoEstLaps);

        _infoTimeRemain = new TextBlock
        {
            FontSize = 9.5,
            Foreground = BRUSH_TEXT,
            FontFamily = new FontFamily("Segoe UI"),
            TextAlignment = TextAlignment.Center,
            Padding = new Thickness(0, 2, 0, 0)
        };
        _rowCanvas.Children.Add(_infoTimeRemain);

        _infoIncidents = new TextBlock
        {
            FontSize = 9.5,
            Foreground = BRUSH_TEXT,
            FontFamily = new FontFamily("Segoe UI"),
            TextAlignment = TextAlignment.Right,
            Padding = new Thickness(0, 2, 4, 0)
        };
        _rowCanvas.Children.Add(_infoIncidents);

        ApplyLayout();
    }

    private void CreateHeaderRow()
    {
        double y = 0;

        _hdrP = AddText(_rowCanvas, "P", _layout.PosX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrNum = AddText(_rowCanvas, "#", _layout.NumX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrCar = AddText(_rowCanvas, "CAR", _layout.CarModelX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrName = AddText(_rowCanvas, "NAME", _layout.NameX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrInfo = AddText(_rowCanvas, "INFO", _layout.InfoX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrInt = AddText(_rowCanvas, "GAP", _layout.IntX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrLast = AddText(_rowCanvas, "LAST", _layout.LastX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrGap = AddText(_rowCanvas, "REL", _layout.GapX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrPosDelta = AddText(_rowCanvas, "Δ", _layout.PosDeltaX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrNat = AddText(_rowCanvas, "NAT", _layout.NatX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);
        _hdrPits = AddText(_rowCanvas, "PIT", _layout.PitsX, y, FONT_HEADER, BRUSH_MUTED, FontWeights.SemiBold);

        // No STATUS header — it floats outside the box

        _hdrSeparator = new Border
        {
            Width = _layout.BoxWidth - PADDING * 2,
            Height = SEPARATOR_HEIGHT,
            Background = BRUSH_MUTED,
            Opacity = 0.3
        };
        Canvas.SetLeft(_hdrSeparator, 0);
        Canvas.SetTop(_hdrSeparator, HEADER_HEIGHT + 2);
        _rowCanvas.Children.Add(_hdrSeparator);

        // Class legend items (pre-allocated, positioned dynamically in UpdateUI)
        double legendY = 2;
        for (int c = 0; c < MAX_LEGEND_ITEMS; c++)
        {
            _legendSwatches[c] = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(1),
                Background = BRUSH_TRANSPARENT,
                Visibility = Visibility.Collapsed
            };
            Canvas.SetTop(_legendSwatches[c], legendY);
            _rowCanvas.Children.Add(_legendSwatches[c]);

            _legendLabels[c] = new TextBlock
            {
                FontSize = 7,
                Foreground = BRUSH_MUTED,
                FontFamily = new FontFamily("Segoe UI"),
                Visibility = Visibility.Collapsed
            };
            Canvas.SetTop(_legendLabels[c], legendY - 1);
            _rowCanvas.Children.Add(_legendLabels[c]);
        }
    }

    private RowElements CreateDataRow(double y)
    {
        var row = new RowElements();

        // Row background (used for player highlight)
        row.Background = new Border
        {
            Width = _layout.BoxWidth - PADDING * 2,
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
        Canvas.SetLeft(row.ClassStripe, 0);
        Canvas.SetTop(row.ClassStripe, y + 2);
        _rowCanvas.Children.Add(row.ClassStripe);

        // All text columns start at x=0 — repositioned dynamically in PopulateRow
        row.Position = CreateRowText(0, y, FONT_DATA, BRUSH_TEXT, FontWeights.Normal);
        row.CarNumber = CreateRowText(0, y, FONT_DATA, BRUSH_MUTED, FontWeights.Normal);
        row.CarModel = CreateRowText(0, y, 9, BRUSH_DIM, FontWeights.Normal);
        row.Name = CreateRowText(0, y, FONT_DATA, BRUSH_TEXT, FontWeights.Normal);

        // Info background — extends license color behind the iRating text
        row.InfoBackground = new Border
        {
            Width = COL_W_INFO,
            Height = ROW_HEIGHT - 2,
            CornerRadius = new CornerRadius(2),
            Background = BRUSH_TRANSPARENT,
            Opacity = 0.25
        };
        Canvas.SetTop(row.InfoBackground, y + 1);
        _rowCanvas.Children.Add(row.InfoBackground);

        // License badge: colored border with letter on top
        row.LicenseBadge = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(2),
            Background = BRUSH_TRANSPARENT
        };
        Canvas.SetTop(row.LicenseBadge, y + 2);
        _rowCanvas.Children.Add(row.LicenseBadge);

        row.LicenseText = new TextBlock
        {
            FontSize = 8.5,
            FontWeight = FontWeights.Bold,
            Foreground = BRUSH_TEXT,
            FontFamily = new FontFamily("Segoe UI"),
            TextAlignment = TextAlignment.Center,
            Width = 14
        };
        Canvas.SetTop(row.LicenseText, y + 3);
        _rowCanvas.Children.Add(row.LicenseText);

        row.DriverInfo = CreateRowText(0, y, FONT_DATA, BRUSH_MUTED, FontWeights.Normal);
        row.Interval = CreateRowText(0, y, FONT_DATA, BRUSH_TEAL, FontWeights.SemiBold);
        row.LastLap = CreateRowText(0, y, FONT_DATA, BRUSH_TEXT, FontWeights.Normal);
        row.Gap = CreateRowText(0, y, FONT_DATA, BRUSH_MUTED, FontWeights.Normal);

        // Optional feature columns
        row.ClosingArrow = CreateRowText(0, y, 10, BRUSH_TEAL, FontWeights.Bold);
        row.PitStops = CreateRowText(0, y, 9, BRUSH_MUTED, FontWeights.Normal);
        row.PositionDelta = CreateRowText(0, y, 9, BRUSH_TEAL, FontWeights.Normal);
        row.Nationality = CreateRowText(0, y, 9, BRUSH_MUTED, FontWeights.Normal);

        // Vertical separator lines between columns (solid through rows)
        row.Separators = new List<Border>();
        for (int s = 0; s < 11; s++) // up to 11 separators between columns (includes optional columns)
        {
            var sep = new Border
            {
                Width = 1,
                Height = ROW_HEIGHT,
                Background = new SolidColorBrush(COLOR_SEPARATOR)
            };
            Canvas.SetTop(sep, y);
            _rowCanvas.Children.Add(sep);
            row.Separators.Add(sep);
        }

        // Status text and background — placed on the MAIN canvas (outside the box)
        double statusCanvasY = PADDING + y; // offset by border padding
        row.StatusBg = new Border
        {
            Width = COL_W_STATUS,
            Height = ROW_HEIGHT,
            Background = new SolidColorBrush(Color.FromArgb(170, 18, 18, 18)),
            CornerRadius = new CornerRadius(3),
            Opacity = STATUS_BG_OPACITY
        };
        Canvas.SetLeft(row.StatusBg, _layout.StatusX);
        Canvas.SetTop(row.StatusBg, statusCanvasY);
        _canvas.Children.Add(row.StatusBg);

        row.Status = new TextBlock
        {
            FontSize = FONT_STATUS,
            Foreground = BRUSH_PIT,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI"),
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 0, 0, 0),
            Height = ROW_HEIGHT,
            LineHeight = ROW_HEIGHT,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };
        Canvas.SetLeft(row.Status, _layout.StatusX);
        Canvas.SetTop(row.Status, statusCanvasY);
        _canvas.Children.Add(row.Status);

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

    public void RecalcHeight()
    {
        // Use actual visible row count if available, otherwise use configured max
        int maxRows = _visibleRowCount > 0 ? _visibleRowCount : MaxAhead + 1 + MaxBehind;
        double infoH = ShowInfoBar ? INFO_BAR_HEIGHT : 0;
        double contentHeight = PADDING + HEADER_HEIGHT + SEPARATOR_HEIGHT + 4 + (maxRows * ROW_HEIGHT) + infoH + PADDING;
        Height = contentHeight;
        _backgroundBorder.Height = contentHeight;
    }

    // ── Core update loop ────────────────────────────────────────────

    protected override void UpdateUI(TelemetryData data)
    {
        // Apply layout changes if toggles were modified
        if (_layoutDirty)
            ApplyLayout();

        // Determine row counts
        int maxAhead, maxBehind;
        if (UseSmartRowCount && data.LivePosition > 0)
        {
            // Estimate total cars from CarIdxPosition array
            int totalCars = CountActiveCars(data);
            (maxAhead, maxBehind) = RelativeCalculator.GetSmartRowCount(
                data.LivePosition, totalCars, MaxAhead + MaxBehind);
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

        // Class color legend in header area
        if (ShowClassLegend)
            UpdateClassLegend(entries);
        else
            HideClassLegend();

        // Always resize to match actual row count + info bar state (avoids empty space)
        int newRowCount = entries.Count;
        _visibleRowCount = newRowCount;
        double infoH = ShowInfoBar ? INFO_BAR_HEIGHT : 0;
        double contentHeight = PADDING + HEADER_HEIGHT + SEPARATOR_HEIGHT + 4
            + (Math.Max(newRowCount, 1) * ROW_HEIGHT) + infoH + PADDING;
        if (Math.Abs(Height - contentHeight) > 0.5)
        {
            Height = contentHeight;
            _backgroundBorder.Height = contentHeight;
        }

        // Info bar: estimated laps, time remaining, incident points
        if (ShowInfoBar)
        {
            double infoY = HEADER_HEIGHT + SEPARATOR_HEIGHT + 4 + (Math.Max(_visibleRowCount, 1) * ROW_HEIGHT);
            double barW = _layout.BoxWidth - PADDING * 2;

            _infoBarBorder.Width = barW;
            Canvas.SetTop(_infoBarBorder, infoY);
            _infoBarBorder.Visibility = Visibility.Visible;

            // Estimated laps remaining (from session data)
            double estLaps = data.SessionLapsRemainEx > 0 ? data.SessionLapsRemainEx : 0;
            _infoEstLaps.Text = estLaps > 0 ? $"~{estLaps:F0} laps" : "";
            Canvas.SetLeft(_infoEstLaps, 0);
            Canvas.SetTop(_infoEstLaps, infoY);
            _infoEstLaps.Visibility = Visibility.Visible;

            // Time remaining
            double timeRemain = data.SessionTimeRemain;
            if (timeRemain > 0 && timeRemain < 86400)
            {
                int hrs = (int)(timeRemain / 3600);
                int mins = (int)((timeRemain % 3600) / 60);
                int secs = (int)(timeRemain % 60);
                _infoTimeRemain.Text = hrs > 0 ? $"{hrs}:{mins:D2}:{secs:D2}" : $"{mins}:{secs:D2}";
            }
            else
            {
                _infoTimeRemain.Text = "";
            }
            _infoTimeRemain.Width = barW;
            Canvas.SetLeft(_infoTimeRemain, 0);
            Canvas.SetTop(_infoTimeRemain, infoY);
            _infoTimeRemain.Visibility = Visibility.Visible;

            // Player incident count (format: Inc: X/17x)
            int playerInc = data.PlayerCarMyIncidentCount;
            int incLimit = 17; // iRacing standard incident limit
            _infoIncidents.Text = $"Inc: {playerInc}/{incLimit}x";
            _infoIncidents.Foreground = playerInc >= incLimit - 4 ? BRUSH_ORANGE
                : playerInc >= incLimit - 8 ? BRUSH_PIT : BRUSH_TEXT;
            _infoIncidents.Width = barW;
            Canvas.SetLeft(_infoIncidents, 0);
            Canvas.SetTop(_infoIncidents, infoY);
            _infoIncidents.Visibility = Visibility.Visible;
        }
        else
        {
            _infoBarBorder.Visibility = Visibility.Collapsed;
            _infoEstLaps.Visibility = Visibility.Collapsed;
            _infoTimeRemain.Visibility = Visibility.Collapsed;
            _infoIncidents.Visibility = Visibility.Collapsed;
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
                PopulateRow(row, entry, i);
            }
            else
            {
                ShowRow(row, false);
            }
        }
    }

    private void PopulateRow(RowElements row, RelativeEntry entry, int rowIndex = 0)
    {
        // ── Reposition elements to match current layout ─────────
        Canvas.SetLeft(row.Position, _layout.PosX);
        Canvas.SetLeft(row.CarNumber, _layout.NumX);
        Canvas.SetLeft(row.CarModel, _layout.CarModelX);
        Canvas.SetLeft(row.Name, _layout.NameX);
        Canvas.SetLeft(row.DriverInfo, _layout.InfoX);
        Canvas.SetLeft(row.Interval, _layout.IntX);
        Canvas.SetLeft(row.LastLap, _layout.LastX);
        Canvas.SetLeft(row.Gap, _layout.GapX);
        // Status is on _canvas — reposition to current layout each frame
        Canvas.SetLeft(row.Status, _layout.StatusX);
        Canvas.SetLeft(row.StatusBg, _layout.StatusX);
        row.Background.Width = _layout.BoxWidth - PADDING * 2;

        // ── Vertical separators between visible columns ─────────
        double[] colEdges = GetVisibleColumnEdges();
        for (int s = 0; s < row.Separators.Count; s++)
        {
            if (s < colEdges.Length)
            {
                Canvas.SetLeft(row.Separators[s], colEdges[s]);
                row.Separators[s].Visibility = Visibility.Visible;
            }
            else
            {
                row.Separators[s].Visibility = Visibility.Collapsed;
            }
        }

        var textBrush = BRUSH_TEXT;
        var mutedBrush = BRUSH_MUTED;

        // Class stripe
        row.ClassStripe.Background = GetClassBrush(entry.CarClassId, false);

        // Player row highlight + alternate row shading
        if (entry.IsPlayer)
        {
            row.Background.Background = BRUSH_PLAYER_BG;
        }
        else if (ShowAlternateRowShading && rowIndex % 2 == 1)
        {
            row.Background.Background = new SolidColorBrush(Color.FromArgb((byte)ALT_ROW_ALPHA, 255, 255, 255));
        }
        else
        {
            row.Background.Background = BRUSH_TRANSPARENT;
        }

        // Position
        int pos = ShowClassPosition ? entry.ClassPosition : entry.OverallPosition;
        row.Position.Text = pos > 0 ? pos.ToString() : "-";
        row.Position.Foreground = entry.IsPlayer ? BRUSH_TEAL : textBrush;

        // Position delta (▲3 gained / ▼2 lost)
        if (ShowPositionChange)
        {
            Canvas.SetLeft(row.PositionDelta, _layout.PosDeltaX);
            row.PositionDelta.Visibility = Visibility.Visible;
            if (!entry.IsPlayer && entry.PositionDelta != 0)
            {
                row.PositionDelta.Text = entry.PositionDelta > 0
                    ? $"▲{entry.PositionDelta}" : $"▼{Math.Abs(entry.PositionDelta)}";
                row.PositionDelta.Foreground = entry.PositionDelta > 0 ? BRUSH_GREEN : BRUSH_RED;
            }
            else
                row.PositionDelta.Text = "";
        }
        else
            row.PositionDelta.Visibility = Visibility.Collapsed;

        // Car number
        row.CarNumber.Text = !string.IsNullOrEmpty(entry.CarNumber) ? $"#{entry.CarNumber}" : "-";
        row.CarNumber.Foreground = mutedBrush;
        row.CarNumber.Visibility = ShowCarNumber ? Visibility.Visible : Visibility.Collapsed;

        // Car model abbreviation
        row.CarModel.Text = !string.IsNullOrEmpty(entry.CarModel) ? entry.CarModel : "";
        row.CarModel.Visibility = ShowCarModel ? Visibility.Visible : Visibility.Collapsed;

        // Nationality (2-letter country code)
        if (ShowNationality)
        {
            Canvas.SetLeft(row.Nationality, _layout.NatX);
            row.Nationality.Visibility = Visibility.Visible;
            row.Nationality.Text = !string.IsNullOrEmpty(entry.CountryCode) ? entry.CountryCode : "";
        }
        else
            row.Nationality.Visibility = Visibility.Collapsed;

        // Name
        string name = !string.IsNullOrEmpty(entry.DriverName)
            ? FormatDriverName(entry.DriverName)
            : "---";
        row.Name.Text = name;
        row.Name.Foreground = textBrush;
        row.Name.FontWeight = entry.IsPlayer ? FontWeights.Bold : FontWeights.Normal;

        // License badge + iRating info
        if (ShowDriverInfo)
        {
            // Colored license badge with letter
            var licBrush = GetLicenseBrush(entry.LicenseClass);
            string licLetter = !string.IsNullOrEmpty(entry.LicenseClass) ? entry.LicenseClass : "?";
            row.LicenseBadge.Background = licBrush;
            row.LicenseBadge.Visibility = Visibility.Visible;
            Canvas.SetLeft(row.LicenseBadge, _layout.InfoX);
            row.LicenseText.Text = licLetter;
            row.LicenseText.Foreground = GetLicenseTextBrush(entry.LicenseClass);
            row.LicenseText.Visibility = Visibility.Visible;
            Canvas.SetLeft(row.LicenseText, _layout.InfoX);

            // Expand license color as background behind iRating
            row.InfoBackground.Background = licBrush;
            row.InfoBackground.Visibility = Visibility.Visible;
            Canvas.SetLeft(row.InfoBackground, _layout.InfoX);

            // iRating value next to badge — colored to match license class
            var licTextBrush = GetLicenseBrush(entry.LicenseClass); // same color as badge
            string irText;
            if (entry.IRating > 0)
            {
                irText = UseFullIRating
                    ? entry.IRating.ToString(CultureInfo.InvariantCulture)
                    : string.Format(CultureInfo.InvariantCulture, "{0:F1}k", entry.IRating / 1000f);
            }
            else
                irText = "-";
            row.DriverInfo.Text = irText;
            row.DriverInfo.Foreground = entry.IRating > 0 ? licTextBrush : BRUSH_MUTED;
            row.DriverInfo.FontSize = UseFullIRating ? 10 : FONT_DATA;
            row.DriverInfo.Visibility = Visibility.Visible;
            Canvas.SetLeft(row.DriverInfo, _layout.InfoX + 17); // offset past badge

            // Size the info background to match column width
            double infoW = UseFullIRating ? COL_W_INFO_FULL : COL_W_INFO;
            row.InfoBackground.Width = infoW;
        }
        else
        {
            row.DriverInfo.Visibility = Visibility.Collapsed;
            row.InfoBackground.Visibility = Visibility.Collapsed;
            row.LicenseBadge.Visibility = Visibility.Collapsed;
            row.LicenseText.Visibility = Visibility.Collapsed;
        }

        // Pit stop count
        if (ShowPitStopCount)
        {
            Canvas.SetLeft(row.PitStops, _layout.PitsX);
            row.PitStops.Visibility = Visibility.Visible;
            row.PitStops.Text = entry.PitStopCount > 0 ? $"{entry.PitStopCount}p" : "";
            row.PitStops.Foreground = entry.PitStopCount >= 2 ? BRUSH_ORANGE : BRUSH_MUTED;
        }
        else
            row.PitStops.Visibility = Visibility.Collapsed;

        // GAP column (toggleable) — gap to car directly ahead
        if (ShowInterval)
        {
            row.Interval.Visibility = Visibility.Visible;
            if (entry.IsPlayer || entry.GapToCarAhead <= 0f)
            {
                row.Interval.Text = "---";
                row.Interval.Foreground = BRUSH_MUTED;
            }
            else
            {
                row.Interval.Text = entry.GapToCarAhead < 100f
                    ? string.Format(CultureInfo.InvariantCulture, "{0:F1}", entry.GapToCarAhead)
                    : ">99";
                row.Interval.Foreground = BRUSH_MUTED;
            }
        }
        else
        {
            row.Interval.Visibility = Visibility.Collapsed;
        }

        // Last lap time
        if (ShowLastLap)
        {
            row.LastLap.Visibility = Visibility.Visible;
            row.LastLap.Text = FormatLapTime(entry.LastLapTime);
            if (entry.IsSessionBest)
                row.LastLap.Foreground = BRUSH_PURPLE;
            else if (entry.IsPersonalBest)
                row.LastLap.Foreground = BRUSH_TEAL;
            else
                row.LastLap.Foreground = mutedBrush;
        }
        else
        {
            row.LastLap.Visibility = Visibility.Collapsed;
        }

        // Closing rate arrow (▲ closing / ▼ pulling away)
        if (ShowClosingRate)
        {
            Canvas.SetLeft(row.ClosingArrow, _layout.CloseX);
            row.ClosingArrow.Visibility = Visibility.Visible;
            if (!entry.IsPlayer && Math.Abs(entry.ClosingRate) > 0.05f)
            {
                row.ClosingArrow.Text = entry.ClosingRate > 0 ? "▲" : "▼";
                row.ClosingArrow.Foreground = entry.ClosingRate > 0 ? BRUSH_GREEN : BRUSH_RED;
            }
            else
                row.ClosingArrow.Text = "";
        }
        else
            row.ClosingArrow.Visibility = Visibility.Collapsed;

        // REL column (always visible) — relative interval to player
        if (entry.IsPlayer)
        {
            row.Gap.Text = "---";
            row.Gap.Foreground = BRUSH_MUTED;
            row.Gap.FontWeight = FontWeights.Normal;
        }
        else
        {
            row.Gap.Text = FormatInterval(entry.IntervalToPlayer, entry.LapDelta);
            // Sector delta: green=closing, red=opening (overrides default teal/orange)
            if (ShowSectorDelta)
                row.Gap.Foreground = entry.IsGapClosing ? BRUSH_GREEN : BRUSH_RED;
            else
                row.Gap.Foreground = entry.IntervalToPlayer > 0 ? BRUSH_ORANGE : BRUSH_TEAL;
            row.Gap.FontWeight = FontWeights.SemiBold;
        }

        // ── Status column: priority-based (OUTSIDE BOX) ────────────
        bool isBlinkOn = (_frameCount / BLINK_HALF_PERIOD) % 2 == 0;
        string statusText = string.Empty;
        SolidColorBrush statusBrush = BRUSH_PIT;
        bool showStatusBg = false;

        if (entry.HasMeatball && !entry.IsPlayer)
        {
            statusText = isBlinkOn ? "MEATBALL" : "";
            statusBrush = BRUSH_MEATBALL;
            showStatusBg = true;
        }
        else if (entry.HasBlackFlag && !entry.IsPlayer)
        {
            statusText = isBlinkOn ? "BLACK" : "";
            statusBrush = BRUSH_WHITE; // white text on black background
            showStatusBg = true;
        }
        else if (entry.PitState == PitStatus.Pitting && !entry.IsPlayer)
        {
            statusText = "PITTING";
            statusBrush = BRUSH_PIT;
            showStatusBg = true;
        }
        else if (entry.PitState == PitStatus.InPit && !entry.IsPlayer)
        {
            // Show TOWED if driver was towed in
            if (entry.WasTowed)
            {
                statusText = FormatTowTimer(entry.PitStallDuration);
            }
            else
            {
                statusText = FormatBoxTimer(entry.PitStallDuration);
            }
            statusBrush = BRUSH_PIT;
            showStatusBg = true;
        }
        else if (entry.PitState == PitStatus.ExitingPit && !entry.IsPlayer)
        {
            // Blink the final BOX time for 3 seconds
            if (entry.FinalBoxDuration > 0f && entry.ExitingPitDuration < 3.0f)
            {
                statusText = isBlinkOn ? FormatBoxTimer(entry.FinalBoxDuration) : "";
            }
            else
            {
                statusText = isBlinkOn ? "EXITING" : "";
            }
            statusBrush = BRUSH_PIT;
            showStatusBg = true;
        }
        else if (entry.IsOnOutLap && !entry.IsPlayer)
        {
            statusText = "OUTLAP";
            statusBrush = BRUSH_WHITE;
            showStatusBg = true;
        }
        else if (entry.HasRecentIncident && !entry.IsPlayer)
        {
            // Show incident delta severity — highlight 2x+ more prominently
            int delta = entry.IncidentDelta;
            string incText;
            if (delta >= 2)
            {
                incText = $"INC +{delta}x";
                statusBrush = BRUSH_MEATBALL; // orange-red for significant incidents (2-4x)
            }
            else
            {
                incText = entry.IncidentCount > 0 ? $"INC {entry.IncidentCount}x" : "INC";
                statusBrush = BRUSH_ORANGE;
            }
            statusText = isBlinkOn ? incText : "";
            showStatusBg = true;
        }
        else if (entry.IsOffTrack && !entry.IsPlayer && entry.OffTrackDuration >= 0.5f)
        {
            bool blinkVisible = entry.OffTrackDuration < 2.0f || isBlinkOn;
            statusText = blinkVisible ? "OFF TRACK" : "";
            statusBrush = BRUSH_ORANGE;
            showStatusBg = true;
        }

        row.Status.Text = statusText;
        row.Status.Foreground = statusBrush;
        row.StatusBg.Background = entry.HasBlackFlag && !entry.IsPlayer && showStatusBg
            ? BRUSH_STATUS_BG_BLACK : BRUSH_STATUS_BG_DEFAULT;
        row.StatusBg.Visibility = showStatusBg && !string.IsNullOrEmpty(statusText)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Get column boundary X positions for vertical separators.</summary>
    private double[] GetVisibleColumnEdges()
    {
        var edges = new List<double>();
        // After POS column
        edges.Add(_layout.PosX + COL_W_POS - 2);
        // After POS DELTA column (if visible)
        if (ShowPositionChange) edges.Add(_layout.PosDeltaX + COL_W_POS_DELTA - 2);
        // After NUM column (if visible)
        if (ShowCarNumber) edges.Add(_layout.NumX + COL_W_NUM - 2);
        // After CAR MODEL column (if visible)
        if (ShowCarModel) edges.Add(_layout.CarModelX + COL_W_CAR_MODEL - 2);
        // After NAT column (if visible)
        if (ShowNationality) edges.Add(_layout.NatX + COL_W_NAT - 2);
        // After NAME column
        edges.Add(_layout.NameX + COL_W_NAME - 2);
        // After INFO column (if visible)
        if (ShowDriverInfo) edges.Add(_layout.InfoX + COL_W_INFO - 2);
        // After PITS column (if visible)
        if (ShowPitStopCount) edges.Add(_layout.PitsX + COL_W_PITS - 2);
        // After INT column (if visible)
        if (ShowInterval) edges.Add(_layout.IntX + COL_W_INT - 2);
        // After LAST column (if visible)
        if (ShowLastLap) edges.Add(_layout.LastX + COL_W_LAST - 2);
        // After CLOSE column (if visible)
        if (ShowClosingRate) edges.Add(_layout.CloseX + COL_W_CLOSE - 2);
        return edges.ToArray();
    }

    /// <summary>Format BOX timer as "BOX M:SS.s" (1 decimal ms).</summary>
    private static string FormatBoxTimer(float seconds)
    {
        if (seconds <= 0f) return "BOX 0:00.0";
        int totalMs = (int)(seconds * 1000f);
        int min = totalMs / 60000;
        int sec = (totalMs % 60000) / 1000;
        int tenths = (totalMs % 1000) / 100;
        return $"BOX {min}:{sec:D2}.{tenths}";
    }

    /// <summary>Format TOW timer as "TOW M:SS" for towed cars.</summary>
    private static string FormatTowTimer(float seconds)
    {
        if (seconds <= 0f) return "TOW 0:00";
        int totalSec = (int)seconds;
        int min = totalSec / 60;
        int sec = totalSec % 60;
        return $"TOW {min}:{sec:D2}";
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
        row.CarModel.Visibility = vis;
        row.Name.Visibility = vis;
        row.DriverInfo.Visibility = vis;
        row.InfoBackground.Visibility = vis;
        row.LicenseBadge.Visibility = vis;
        row.LicenseText.Visibility = vis;
        row.Interval.Visibility = vis;
        row.Gap.Visibility = vis;
        row.LastLap.Visibility = vis;
        // New feature elements
        row.ClosingArrow.Visibility = vis;
        row.PitStops.Visibility = vis;
        row.PositionDelta.Visibility = vis;
        row.Nationality.Visibility = vis;
        // Status and StatusBg are on the main canvas — hide when row hidden
        row.Status.Visibility = vis;
        if (!visible)
            row.StatusBg.Visibility = Visibility.Collapsed;
        // Separators
        foreach (var sep in row.Separators)
            sep.Visibility = vis;
    }

    // ── Formatting helpers ──────────────────────────────────────────

    /// <summary>Format driver name according to DriverNameFormat setting.</summary>
    private string FormatDriverName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "---";

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "---";

        string formatted = DriverNameFormat switch
        {
            NameFormat.FullName => fullName.Trim(),
            NameFormat.InitialsOnly => string.Join(".", parts.Select(p => p[..1].ToUpperInvariant())) + ".",
            NameFormat.FirstThreeLetters => parts[0].Length >= 3
                ? parts[0][..3].ToUpperInvariant()
                : parts[0].ToUpperInvariant(),
            NameFormat.LastNameOnly => parts[^1],
            NameFormat.FirstNameOnly => parts[0],
            NameFormat.ThreeLetterCode => parts[^1].Length >= 3
                ? parts[^1][..3].ToUpperInvariant()
                : parts[^1].ToUpperInvariant(),
            NameFormat.LastCommaFirst => parts.Length == 1
                ? parts[0]
                : $"{parts[^1]}, {parts[0][..1]}.",
            NameFormat.FirstLast4 => parts.Length == 1
                ? parts[0]
                : $"{parts[0][..1]}.{parts[^1][..Math.Min(4, parts[^1].Length)]}",
            NameFormat.FirstNameLastInit => parts.Length == 1
                ? parts[0]
                : $"{parts[0]} {parts[^1][..1]}.",
            NameFormat.UpperLastName => parts[^1].ToUpperInvariant(),
            NameFormat.InitialLast3 => parts.Length == 1
                ? parts[0]
                : $"{parts[0][..1]}.{parts[^1][..Math.Min(3, parts[^1].Length)]}",
            NameFormat.LastSpaceFirst => parts.Length == 1
                ? parts[0]
                : $"{parts[^1]} {parts[0][..1]}",
            NameFormat.CompactNoPrefix => parts.Length == 1
                ? parts[0]
                : $"{parts[0][..1]}{parts[^1]}",
            _ => parts.Length == 1
                ? parts[0]
                : $"{parts[0][..1].ToUpperInvariant()}.{parts[^1]}"
        };

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

    /// <summary>Format interval as "+3.2" / "-1.5" — always shows time, with lap delta as suffix.</summary>
    private static string FormatInterval(float intervalSeconds, int lapDelta)
    {
        // Always show relative time gap (never just "+1L")
        string sign = intervalSeconds >= 0 ? "+" : "";
        string time = string.Format(CultureInfo.InvariantCulture, "{0}{1:F1}", sign, intervalSeconds);

        // Append lap indicator as suffix for lapped cars
        if (Math.Abs(lapDelta) >= 1)
        {
            string lapStr = lapDelta > 0 ? $"+{lapDelta}L" : $"{lapDelta}L";
            return $"{time} {lapStr}";
        }

        return time;
    }

    // ── Class legend helpers ──────────────────────────────────────────

    private void UpdateClassLegend(List<RelativeEntry> entries)
    {
        // Gather unique class IDs with car counts
        var classCount = new Dictionary<int, int>();
        foreach (var e in entries)
        {
            if (e.CarClassId > 0)
            {
                if (!classCount.ContainsKey(e.CarClassId))
                    classCount[e.CarClassId] = 0;
                classCount[e.CarClassId]++;
            }
        }

        int idx = 0;
        double x = _layout.BoxWidth - PADDING * 2; // right-align from box edge
        foreach (var kvp in classCount)
        {
            if (idx >= MAX_LEGEND_ITEMS) break;

            // Right-align: count label, then color swatch to its left
            string label = kvp.Value.ToString();
            x -= 14;
            _legendLabels[idx].Text = label;
            Canvas.SetLeft(_legendLabels[idx], x);
            _legendLabels[idx].Visibility = Visibility.Visible;

            x -= 10;
            _legendSwatches[idx].Background = GetClassBrush(kvp.Key, false);
            Canvas.SetLeft(_legendSwatches[idx], x);
            _legendSwatches[idx].Visibility = Visibility.Visible;

            x -= 3; // gap between legend items
            idx++;
        }

        // Hide unused slots
        for (; idx < MAX_LEGEND_ITEMS; idx++)
        {
            _legendSwatches[idx].Visibility = Visibility.Collapsed;
            _legendLabels[idx].Visibility = Visibility.Collapsed;
        }
    }

    private void HideClassLegend()
    {
        for (int i = 0; i < MAX_LEGEND_ITEMS; i++)
        {
            _legendSwatches[i].Visibility = Visibility.Collapsed;
            _legendLabels[i].Visibility = Visibility.Collapsed;
        }
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

    /// <summary>Get iRacing standard license color brush.</summary>
    private static SolidColorBrush GetLicenseBrush(string licenseClass)
    {
        return licenseClass?.ToUpperInvariant() switch
        {
            "A" => BRUSH_LIC_A,
            "B" => BRUSH_LIC_B,
            "C" => BRUSH_LIC_C,
            "D" => BRUSH_LIC_D,
            "R" => BRUSH_LIC_R,
            "PRO" or "WC" => BRUSH_WHITE,
            _ => BRUSH_MUTED
        };
    }

    /// <summary>Get text color for license badge — dark text on light backgrounds for readability.</summary>
    private static SolidColorBrush GetLicenseTextBrush(string licenseClass)
    {
        return licenseClass?.ToUpperInvariant() switch
        {
            "C" or "D" => BRUSH_DARK_TEXT, // dark text on yellow/orange badges
            _ => BRUSH_WHITE               // white text on blue/green/red/pro
        };
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
        public TextBlock CarModel = null!;    // 3-letter car model abbreviation
        public TextBlock Name = null!;
        public Border InfoBackground = null!; // colored background spanning full info column
        public Border LicenseBadge = null!;   // colored background for license letter
        public TextBlock LicenseText = null!;  // license letter on colored badge
        public TextBlock DriverInfo = null!;  // iRating value next to badge
        public TextBlock Interval = null!;
        public TextBlock Gap = null!;
        public TextBlock LastLap = null!;
        public TextBlock Status = null!;      // placed on _canvas (outside box)
        public Border StatusBg = null!;       // semi-transparent bg behind status
        public List<Border> Separators = new(); // vertical column separators
        // New feature elements
        public TextBlock ClosingArrow = null!;    // ▲/▼ closing rate indicator
        public TextBlock PitStops = null!;        // pit stop count
        public TextBlock PositionDelta = null!;   // position change arrows
        public TextBlock Nationality = null!;     // 2-letter country code
    }
}
