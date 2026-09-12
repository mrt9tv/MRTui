using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF.Widgets.RaceStartWidget;

/// <summary>
/// The race start, on its own card.
///
/// Three phases. While the lights are up it shows five squares filling as
/// READY turns to SET, so the eye is already on it. At lights-out the squares
/// go green. Once the start has been measured — on the telemetry thread, by
/// RaceStartDetector — the card shows the reaction time large, with launch
/// time, technique and session best beneath, or JUMP START in red. It then
/// holds for a configurable time and hides.
///
/// This used to be a line in the Proximity Feed, where it lost to the
/// off-tracks and contact of a race start for the six slots the feed has.
/// </summary>
public partial class RaceStartWidget : WidgetBase
{
    #region Constants

    private const double WIDGET_WIDTH = 240;
    private const double WIDGET_HEIGHT = 104;
    private const double PADDING = 10;
    private const double BORDER_RADIUS = 5;
    private const double LIGHT_SIZE = 26;
    private const double LIGHT_GAP = 8;
    private const int LIGHT_COUNT = 5;

    // SessionFlags bits for the light sequence.
    private const uint FlagStartReady = 0x20000000;
    private const uint FlagStartSet = 0x40000000;
    private const uint FlagStartGo = 0x80000000;

    private const int SessionStateParadeLaps = 3;

    /// <summary>How long the green squares stay before the result takes over, if it has not yet.</summary>
    private const double GreenHoldSeconds = 3.0;

    #endregion

    #region Colors

    private static readonly Color COLOR_BG = Color.FromArgb(228, 18, 18, 18);
    private static readonly Color COLOR_TEAL = Color.FromRgb(0, 240, 240);
    private static readonly Color COLOR_TEXT = Color.FromRgb(225, 225, 225);
    private static readonly Color COLOR_MUTED = Color.FromRgb(130, 130, 130);
    private static readonly Color COLOR_LIGHT_OFF = Color.FromRgb(45, 50, 52);
    private static readonly Color COLOR_LIGHT_RED = Color.FromRgb(235, 45, 40);
    private static readonly Color COLOR_LIGHT_GREEN = Color.FromRgb(60, 210, 100);
    private static readonly Color COLOR_GOOD = Color.FromRgb(80, 210, 130);
    private static readonly Color COLOR_WARN = Color.FromRgb(255, 176, 0);
    private static readonly Color COLOR_ALARM = Color.FromRgb(255, 70, 60);

    #endregion

    #region State

    private Canvas _canvas = null!;
    private Border _background = null!;
    private TextBlock _title = null!;
    private StackPanel _lightsRow = null!;
    private readonly Border[] _lights = new Border[LIGHT_COUNT];
    private TextBlock _phaseText = null!;
    private TextBlock _bigValue = null!;
    private TextBlock _detailRow = null!;
    private TextBlock _bestRow = null!;

    private int _shownSequence;
    private DateTime _resultShownAt;
    private DateTime? _greenAt;
    private int _sessionNum = -1;

    #endregion

    #region Settings

    /// <summary>Seconds to keep the result on screen. 0 keeps it until the session changes.</summary>
    public int HoldSeconds { get; set; } = 45;

    /// <summary>Show the five squares while the lights are up.</summary>
    public bool ShowLights { get; set; } = true;

    /// <summary>Show the launch (first movement) time beneath the reaction.</summary>
    public bool ShowLaunch { get; set; } = true;

    /// <summary>Name the technique — clutch, N→gear, throttle, rolling.</summary>
    public bool ShowTechnique { get; set; } = true;

    /// <summary>Show the best reaction since MRT UI started.</summary>
    public bool ShowBest { get; set; } = true;

    #endregion

    public override WidgetType WidgetType => WidgetType.RaceStart;

    /// <summary>The lights are a fraction-of-a-second affair; render every frame while they matter.</summary>
    protected override int UpdateIntervalTicks => 1;

    public RaceStartWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        BuildVisuals();
        LoadSettings();
    }

    private void BuildVisuals()
    {
        Width = WIDGET_WIDTH;
        Height = WIDGET_HEIGHT;

        _canvas = new Canvas { Background = Brushes.Transparent, Visibility = Visibility.Collapsed };

        _background = new Border
        {
            Width = WIDGET_WIDTH,
            Height = WIDGET_HEIGHT,
            CornerRadius = new CornerRadius(BORDER_RADIUS),
            Background = BrushCache.Get(COLOR_BG.A, COLOR_BG.R, COLOR_BG.G, COLOR_BG.B),
            BorderBrush = BrushCache.Get(COLOR_TEAL),
            BorderThickness = new Thickness(1),
        };
        _canvas.Children.Add(_background);

        _title = AddText("RACE START", PADDING, PADDING, 10, COLOR_TEAL, FontWeights.Bold);

        // Five square lights, centred.
        _lightsRow = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < LIGHT_COUNT; i++)
        {
            _lights[i] = new Border
            {
                Width = LIGHT_SIZE,
                Height = LIGHT_SIZE,
                CornerRadius = new CornerRadius(4),
                Background = BrushCache.Get(COLOR_LIGHT_OFF),
                Margin = new Thickness(i == 0 ? 0 : LIGHT_GAP, 0, 0, 0),
            };
            _lightsRow.Children.Add(_lights[i]);
        }
        double lightsWidth = LIGHT_COUNT * LIGHT_SIZE + (LIGHT_COUNT - 1) * LIGHT_GAP;
        Canvas.SetLeft(_lightsRow, (WIDGET_WIDTH - lightsWidth) / 2);
        Canvas.SetTop(_lightsRow, 34);
        _canvas.Children.Add(_lightsRow);

        _phaseText = AddText("", 0, 70, 13, COLOR_MUTED, FontWeights.Bold);
        _phaseText.Width = WIDGET_WIDTH;
        _phaseText.TextAlignment = TextAlignment.Center;

        _bigValue = AddText("", 0, 26, 34, COLOR_TEXT, FontWeights.Bold);
        _bigValue.Width = WIDGET_WIDTH;
        _bigValue.TextAlignment = TextAlignment.Center;

        _detailRow = AddText("", 0, 68, 11, COLOR_TEXT, FontWeights.Normal);
        _detailRow.Width = WIDGET_WIDTH;
        _detailRow.TextAlignment = TextAlignment.Center;

        _bestRow = AddText("", 0, 84, 10, COLOR_MUTED, FontWeights.Normal);
        _bestRow.Width = WIDGET_WIDTH;
        _bestRow.TextAlignment = TextAlignment.Center;

        Content = _canvas;
    }

    private TextBlock AddText(string text, double x, double y, double size, Color color, FontWeight weight)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Foreground = BrushCache.Get(color),
            FontFamily = new FontFamily("Consolas, Segoe UI"),
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        _canvas.Children.Add(tb);
        return tb;
    }

    // ── Settings ──────────────────────────────────────────────────────

    private void LoadSettings()
    {
        HoldSeconds = (int)ReadDouble("holdSeconds", HoldSeconds, 0, 600);
        ShowLights = ReadBool("showLights", ShowLights);
        ShowLaunch = ReadBool("showLaunch", ShowLaunch);
        ShowTechnique = ReadBool("showTechnique", ShowTechnique);
        ShowBest = ReadBool("showBest", ShowBest);
    }

    protected override void SaveWidgetSettings()
    {
        Config.Settings["holdSeconds"] = HoldSeconds;
        Config.Settings["showLights"] = ShowLights;
        Config.Settings["showLaunch"] = ShowLaunch;
        Config.Settings["showTechnique"] = ShowTechnique;
        Config.Settings["showBest"] = ShowBest;
    }

    private bool ReadBool(string key, bool fallback)
    {
        if (!Config.Settings.TryGetValue(key, out var v)) return fallback;
        return v switch
        {
            System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.True,
            bool b => b,
            _ => fallback,
        };
    }

    private double ReadDouble(string key, double fallback, double min, double max)
    {
        if (!Config.Settings.TryGetValue(key, out var v)) return fallback;
        double d = v switch
        {
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetDouble(),
            int i => i,
            double dd => dd,
            float f => f,
            _ => fallback,
        };
        return Math.Clamp(d, min, max);
    }

    // ── Render ────────────────────────────────────────────────────────

    protected override void UpdateUI(TelemetryData data)
    {
        // The detector clears its result on a session change; mirror that so a
        // stale card cannot outlive the session it belongs to.
        if (data.SessionNum != _sessionNum)
        {
            _sessionNum = data.SessionNum;
            _shownSequence = 0;
            _greenAt = null;
        }

        uint flags = data.SessionFlags;
        bool lightsUp = (flags & (FlagStartReady | FlagStartSet)) != 0;
        bool go = (flags & FlagStartGo) != 0;
        var result = data.RaceStart;
        bool hasResult = result != null && result.Sequence > 0;

        if (go && _greenAt == null) _greenAt = DateTime.UtcNow;
        if (!go && !lightsUp) _greenAt = null;

        if (hasResult && result!.Sequence != _shownSequence)
        {
            _shownSequence = result.Sequence;
            _resultShownAt = DateTime.UtcNow;
        }

        bool resultLive = hasResult && result!.Sequence == _shownSequence
            && (HoldSeconds == 0 || (DateTime.UtcNow - _resultShownAt).TotalSeconds < HoldSeconds);

        bool greenLive = go && _greenAt != null
            && (DateTime.UtcNow - _greenAt.Value).TotalSeconds < GreenHoldSeconds;

        if (resultLive)
        {
            ShowResult(result!);
        }
        else if (ShowLights && (lightsUp || greenLive))
        {
            ShowLightsPhase(flags);
        }
        else if (ShowLights && data.SessionState == SessionStateParadeLaps)
        {
            ShowPace();
        }
        else
        {
            UiUpdate.SetVisibility(_canvas, Visibility.Collapsed);
            return;
        }

        UiUpdate.SetVisibility(_canvas, Visibility.Visible);
    }

    private void ShowLightsPhase(uint flags)
    {
        bool go = (flags & FlagStartGo) != 0;
        bool set = (flags & FlagStartSet) != 0;

        // READY: squares dark. SET: all red. GO: all green.
        var lit = go ? COLOR_LIGHT_GREEN : set ? COLOR_LIGHT_RED : COLOR_LIGHT_OFF;
        for (int i = 0; i < LIGHT_COUNT; i++)
            _lights[i].Background = BrushCache.Get(lit);

        UiUpdate.SetVisibility(_lightsRow, Visibility.Visible);
        UiUpdate.SetVisibility(_bigValue, Visibility.Collapsed);
        UiUpdate.SetVisibility(_detailRow, Visibility.Collapsed);
        UiUpdate.SetVisibility(_bestRow, Visibility.Collapsed);
        UiUpdate.SetVisibility(_phaseText, Visibility.Visible);

        UiUpdate.SetText(_phaseText, go ? "GO" : set ? "SET" : "READY");
        UiUpdate.SetForeground(_phaseText, BrushCache.Get(go ? COLOR_LIGHT_GREEN : set ? COLOR_LIGHT_RED : COLOR_MUTED));
        UiUpdate.SetText(_title, "RACE START");
    }

    private void ShowPace()
    {
        for (int i = 0; i < LIGHT_COUNT; i++)
            _lights[i].Background = BrushCache.Get(COLOR_LIGHT_OFF);

        UiUpdate.SetVisibility(_lightsRow, Visibility.Visible);
        UiUpdate.SetVisibility(_bigValue, Visibility.Collapsed);
        UiUpdate.SetVisibility(_detailRow, Visibility.Collapsed);
        UiUpdate.SetVisibility(_bestRow, Visibility.Collapsed);
        UiUpdate.SetVisibility(_phaseText, Visibility.Visible);
        UiUpdate.SetText(_phaseText, "PACE LAPS");
        UiUpdate.SetForeground(_phaseText, BrushCache.Get(COLOR_WARN));
        UiUpdate.SetText(_title, "RACE START");
    }

    private void ShowResult(RaceStartResult r)
    {
        UiUpdate.SetVisibility(_lightsRow, Visibility.Collapsed);
        UiUpdate.SetVisibility(_phaseText, Visibility.Collapsed);
        UiUpdate.SetVisibility(_bigValue, Visibility.Visible);
        UiUpdate.SetVisibility(_detailRow, Visibility.Visible);
        UiUpdate.SetVisibility(_bestRow, ShowBest ? Visibility.Visible : Visibility.Collapsed);

        if (r.JumpStart)
        {
            UiUpdate.SetText(_title, "RACE START");
            UiUpdate.SetText(_bigValue, "JUMP START");
            UiUpdate.SetForeground(_bigValue, BrushCache.Get(COLOR_ALARM));
            UiUpdate.SetText(_detailRow, "moved before the lights went out");
            UiUpdate.SetForeground(_detailRow, BrushCache.Get(COLOR_MUTED));
            UiUpdate.SetText(_bestRow, "");
            return;
        }

        if (r.FlatAtGreen)
        {
            UiUpdate.SetText(_title, "RACE START");
            UiUpdate.SetText(_bigValue, "GREEN");
            UiUpdate.SetForeground(_bigValue, BrushCache.Get(COLOR_LIGHT_GREEN));
            UiUpdate.SetText(_detailRow, "rolling · already flat at the green");
            UiUpdate.SetForeground(_detailRow, BrushCache.Get(COLOR_MUTED));
            UiUpdate.SetText(_bestRow, BestLine(r));
            return;
        }

        UiUpdate.SetText(_title, "REACTION");
        UiUpdate.SetText(_bigValue, r.ReactionSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s");
        UiUpdate.SetForeground(_bigValue, BrushCache.Get(
            r.ReactionSeconds <= 0.30f ? COLOR_GOOD :
            r.ReactionSeconds <= 0.50f ? COLOR_TEXT : COLOR_WARN));

        var detail = new System.Text.StringBuilder();
        if (ShowLaunch && r.LaunchSeconds > 0)
            detail.Append("launch ").Append(r.LaunchSeconds.ToString("0.00", CultureInfo.InvariantCulture)).Append(" s");
        if (ShowTechnique && r.TechniqueLabel.Length > 0)
        {
            if (detail.Length > 0) detail.Append("  ·  ");
            detail.Append(r.TechniqueLabel);
        }
        UiUpdate.SetText(_detailRow, detail.ToString());
        UiUpdate.SetForeground(_detailRow, BrushCache.Get(COLOR_TEXT));
        UiUpdate.SetText(_bestRow, BestLine(r));
    }

    private static string BestLine(RaceStartResult r) =>
        r.SessionBestSeconds > 0
            ? "best " + r.SessionBestSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s"
            : "";
}
