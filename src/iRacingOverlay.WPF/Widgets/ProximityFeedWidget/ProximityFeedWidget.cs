using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF.Widgets.ProximityFeedWidget;

/// <summary>
/// Proximity Feed Widget — animated event notifications for nearby cars (14s ahead, 7s behind).
/// Lightweight vertical feed showing collisions, off-tracks, pitting, stopped cars, flags.
/// Events slide in, display briefly, then fade out. Most severe events at top.
/// Includes a persistent drag handle for repositioning even when feed is empty.
///
/// MRT theme: dark translucent background, severity-colored left stripe, compact text.
/// </summary>
public partial class ProximityFeedWidget : WidgetBase
{
    #region Constants

    private const double WIDGET_WIDTH = 220;
    private const double ROW_HEIGHT = 28;
    private const double ROW_GAP = 3;
    private const double PADDING = 6;
    private const double BORDER_RADIUS = 5;
    private const double STRIPE_WIDTH = 3;
    private const int MAX_VISIBLE_ROWS = 6;

    // Animation durations
    private const double FADE_IN_MS = 150;
    private const double FADE_OUT_MS = 300;

    #endregion

    #region Colors

    private static readonly Color COLOR_BG = Color.FromArgb(220, 18, 18, 18);
    private static readonly Color COLOR_TEXT = Color.FromRgb(200, 200, 200);
    private static readonly Color COLOR_MUTED = Color.FromRgb(120, 120, 120);

    // Severity colors (fallback)
    private static readonly Color COLOR_INFO = Color.FromRgb(0, 150, 180);     // teal
    private static readonly Color COLOR_WARNING = Color.FromRgb(255, 180, 0);  // amber
    private static readonly Color COLOR_DANGER = Color.FromRgb(255, 60, 60);   // red
    private static readonly Color COLOR_CRITICAL = Color.FromRgb(255, 20, 20); // bright red

    // Per-event-type colors for richer visual differentiation
    private static readonly Color COLOR_OFF_TRACK = Color.FromRgb(255, 160, 0);     // orange
    private static readonly Color COLOR_COLLISION = Color.FromRgb(255, 30, 30);      // bright red
    private static readonly Color COLOR_SLOW_CAR = Color.FromRgb(255, 200, 40);      // gold/yellow — static
    private static readonly Color COLOR_STOPPED = Color.FromRgb(255, 50, 50);        // red — blink
    private static readonly Color COLOR_PITTING = Color.FromRgb(255, 200, 50);       // yellow — static (like RelativeWidget)
    private static readonly Color COLOR_IN_BOX = Color.FromRgb(0, 140, 160);         // dark teal (not displayed)
    private static readonly Color COLOR_PIT_EXIT = Color.FromRgb(255, 200, 50);      // blinking yellow (like RelativeWidget)
    private static readonly Color COLOR_MEATBALL = Color.FromRgb(255, 100, 0);       // orange-red
    private static readonly Color COLOR_BLACK_FLAG = Color.FromRgb(240, 240, 240);   // white-on-black (special bg)
    private static readonly Color COLOR_TOWED = Color.FromRgb(180, 80, 220);         // purple
    private static readonly Color COLOR_LOCAL_YELLOW = Color.FromRgb(255, 230, 0);   // bright yellow
    private static readonly Color COLOR_CHAOS_ORANGE = Color.FromRgb(255, 140, 0);  // CHAOS ZONE orange phase
    private static readonly Color COLOR_CHAOS_RED = Color.FromRgb(255, 40, 20);     // CHAOS ZONE red phase
    private static readonly Color COLOR_SPIN = Color.FromRgb(220, 120, 0);           // deep orange
    private static readonly Color COLOR_OVERTAKING = Color.FromRgb(255, 140, 0);     // orange for higher-class overtake imminent
    private static readonly Color COLOR_DISQUALIFIED = Color.FromRgb(140, 0, 0);     // dark red
    private static readonly Color COLOR_BLUE_FLAG = Color.FromRgb(0, 100, 255);      // blue
    private static readonly Color COLOR_SAFETY_CAR = Color.FromRgb(255, 230, 0);     // bright yellow
    private static readonly Color COLOR_START_SEQ = Color.FromRgb(0, 200, 80);       // green (GO!)
    private static readonly Color COLOR_CHECKERED = Color.FromRgb(255, 255, 255);    // white
    private static readonly Color COLOR_RED_FLAG = Color.FromRgb(255, 0, 0);         // red
    private static readonly Color COLOR_WHITE_FLAG = Color.FromRgb(255, 255, 255);    // white (final lap)
    private static readonly Color COLOR_PACE_FLAG = Color.FromRgb(200, 200, 80);     // warm yellow-green
    private static readonly Color COLOR_INCOMING_FAST = Color.FromRgb(255, 160, 0);   // orange — approaching warning
    private static readonly Color COLOR_INCIDENT = Color.FromRgb(255, 40, 0);        // red-orange — escalated incident

    // Session alerts (own car / conditions) — deliberately distinct from the
    // car-event palette above so they read as a different class of information.
    private static readonly Color COLOR_RAIN = Color.FromRgb(90, 170, 255);          // blue — weather
    private static readonly Color COLOR_ENGINE = Color.FromRgb(255, 80, 40);         // red-orange — mechanical
    private static readonly Color COLOR_NETWORK = Color.FromRgb(190, 140, 255);      // violet — connection
    private static readonly Color COLOR_FFB = Color.FromRgb(140, 200, 210);          // muted cyan — advisory
    private static readonly Color COLOR_TYRES = Color.FromRgb(255, 190, 90);         // amber — strategy
    private static readonly Color COLOR_MY_INCIDENT = Color.FromRgb(255, 120, 120);  // soft red — own incidents
    private static readonly Color COLOR_PIT_LANE = Color.FromRgb(255, 200, 50);      // yellow — matches pitting
    private static readonly Color COLOR_REACTION = Color.FromRgb(120, 230, 160);     // mint — the start went fine
    private static readonly Color COLOR_JUMP_START = Color.FromRgb(255, 60, 60);     // red — penalty incoming

    private static readonly SolidColorBrush BRUSH_TEXT = new(COLOR_TEXT);
    private static readonly SolidColorBrush BRUSH_MUTED = new(COLOR_MUTED);

    // Text blink animation constants
    private const double TEXT_BLINK_INTERVAL_MS = 500; // 0.5s half-period for text blink
    private int _blinkFrame;
    private const int BLINK_HALF_PERIOD = 30; // frames per half-cycle at 60Hz

    #endregion

    #region State

    private Canvas _canvas = null!;
    private Border _backgroundBorder = null!;
    private Border _dragHandle = null!;
    private StackPanel _feedStack = null!;
    private readonly NearbyEventDetector _detector = new();
    private readonly Dictionary<long, Border> _eventRows = new();
    private readonly HashSet<long> _knownEventIds = new();

    #endregion

    #region Settings

    /// <summary>Maximum events shown at once (1-6).</summary>
    public int MaxVisibleEvents { get; set; } = MAX_VISIBLE_ROWS;

    /// <summary>Show direction indicator (▲ AHEAD / ▼ BEHIND).</summary>
    public bool ShowDirection { get; set; } = true;

    /// <summary>Show interval time (e.g., "+3.2s").</summary>
    public bool ShowInterval { get; set; } = true;

    /// <summary>Feed grows upward (newest at bottom, older slides up).</summary>
    public bool GrowUpward { get; set; } = false;

    /// <summary>Show higher-class overtaking imminent alerts.</summary>
    public bool ShowOvertakingAlert { get; set; } = true;

    /// <summary>Show race start sequence events (READY/SET/GO).</summary>
    public bool ShowStartSequence { get; set; } = true;

    /// <summary>Show checkered flag event.</summary>
    public bool ShowCheckeredFlag { get; set; } = true;

    /// <summary>Show pace car / caution events (safety car, pace flags).</summary>
    public bool ShowPaceFlags { get; set; } = true;

    /// <summary>Show the player's own incident count going up.</summary>
    public bool ShowMyIncidents { get; set; } = true;

    /// <summary>Announce the reaction and launch time after the start lights.</summary>
    public bool ShowRaceStart { get; set; } = true;

    /// <summary>Detection range ahead of the player in seconds (1-30, default 12).</summary>
    public float DetectionAheadSeconds { get; set; } = 12.0f;

    /// <summary>Detection range behind the player in seconds (1-15, default 6).</summary>
    public float DetectionBehindSeconds { get; set; } = 6.0f;

    /// <summary>Whether stopped/slow suppression is active (formation lap or race start grace).</summary>
    /// <remarks>
    /// Replaces the old _playerHasCrossedSF approach which suppressed until a full lap
    /// was completed — too aggressive on long tracks. Now uses session state + detector
    /// grace period for a tighter, more accurate suppression window.
    /// </remarks>
    private bool _isFormationPhase;

    // iRacing SessionState constants (mirrored from NearbyEventDetector for UI logic)
    private const int SESSION_STATE_PARADE_LAPS = 3;
    private const int SESSION_STATE_RACING = 4;

    #endregion

    public override WidgetType WidgetType => WidgetType.ProximityFeed;

    /// <summary>Update at 30Hz (every 2nd tick) — event feed is text-based, doesn't need 60Hz.</summary>
    protected override int UpdateIntervalTicks => 2;

    /// <summary>Current background alpha (0–255) based on BackgroundOpacity.</summary>
    private byte _bgAlpha = 220;

    /// <inheritdoc/>
    protected override void OnBackgroundOpacityChanged(double opacity)
    {
        _bgAlpha = (byte)(220 * opacity);
    }

    public ProximityFeedWidget(
        ITelemetryService telemetryService,
        WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        InitializeWidget();
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (Config.Settings.TryGetValue("maxVisibleEvents", out var v1))
        {
            if (v1 is JsonElement je1 && je1.TryGetInt32(out var n)) MaxVisibleEvents = Math.Clamp(n, 1, MAX_VISIBLE_ROWS);
            else if (v1 is int i1) MaxVisibleEvents = Math.Clamp(i1, 1, MAX_VISIBLE_ROWS);
        }
        if (Config.Settings.TryGetValue("showDirection", out var v2))
        {
            if (v2 is JsonElement je2) ShowDirection = je2.ValueKind == JsonValueKind.True;
            else if (v2 is bool b2) ShowDirection = b2;
        }
        if (Config.Settings.TryGetValue("showInterval", out var v3))
        {
            if (v3 is JsonElement je3) ShowInterval = je3.ValueKind == JsonValueKind.True;
            else if (v3 is bool b3) ShowInterval = b3;
        }
        if (Config.Settings.TryGetValue("growUpward", out var v4))
        {
            if (v4 is JsonElement je4) GrowUpward = je4.ValueKind == JsonValueKind.True;
            else if (v4 is bool b4) GrowUpward = b4;
        }
        if (Config.Settings.TryGetValue("showOvertakingAlert", out var v5))
        {
            if (v5 is JsonElement je5) ShowOvertakingAlert = je5.ValueKind == JsonValueKind.True;
            else if (v5 is bool b5) ShowOvertakingAlert = b5;
        }
        if (Config.Settings.TryGetValue("showStartSequence", out var v6))
        {
            if (v6 is JsonElement je6) ShowStartSequence = je6.ValueKind == JsonValueKind.True;
            else if (v6 is bool b6) ShowStartSequence = b6;
        }
        if (Config.Settings.TryGetValue("showCheckeredFlag", out var v7))
        {
            if (v7 is JsonElement je7) ShowCheckeredFlag = je7.ValueKind == JsonValueKind.True;
            else if (v7 is bool b7) ShowCheckeredFlag = b7;
        }
        if (Config.Settings.TryGetValue("showPaceFlags", out var v8))
        {
            if (v8 is JsonElement je8) ShowPaceFlags = je8.ValueKind == JsonValueKind.True;
            else if (v8 is bool b8) ShowPaceFlags = b8;
        }
        if (Config.Settings.TryGetValue("showMyIncidents", out var v11))
        {
            if (v11 is JsonElement je11) ShowMyIncidents = je11.ValueKind == JsonValueKind.True;
            else if (v11 is bool b11) ShowMyIncidents = b11;
        }
        if (Config.Settings.TryGetValue("showRaceStart", out var v12))
        {
            if (v12 is JsonElement je12) ShowRaceStart = je12.ValueKind == JsonValueKind.True;
            else if (v12 is bool b12) ShowRaceStart = b12;
        }
        if (Config.Settings.TryGetValue("detectionAhead", out var v9))
        {
            if (v9 is JsonElement je9 && je9.TryGetDouble(out var d9)) DetectionAheadSeconds = (float)Math.Clamp(d9, 1.0, 30.0);
            else if (v9 is float f9) DetectionAheadSeconds = Math.Clamp(f9, 1.0f, 30.0f);
            else if (v9 is double dd9) DetectionAheadSeconds = (float)Math.Clamp(dd9, 1.0, 30.0);
        }
        if (Config.Settings.TryGetValue("detectionBehind", out var v10))
        {
            if (v10 is JsonElement je10 && je10.TryGetDouble(out var d10)) DetectionBehindSeconds = (float)Math.Clamp(d10, 1.0, 15.0);
            else if (v10 is float f10) DetectionBehindSeconds = Math.Clamp(f10, 1.0f, 15.0f);
            else if (v10 is double dd10) DetectionBehindSeconds = (float)Math.Clamp(dd10, 1.0, 15.0);
        }
        // Apply detection range to detector
        _detector.DetectionAheadSeconds = DetectionAheadSeconds;
        _detector.DetectionBehindSeconds = DetectionBehindSeconds;
    }

    protected override void SaveWidgetSettings() => SaveSettings();

    public void SaveSettings()
    {
        Config.Settings["maxVisibleEvents"] = MaxVisibleEvents;
        Config.Settings["showDirection"] = ShowDirection;
        Config.Settings["showInterval"] = ShowInterval;
        Config.Settings["growUpward"] = GrowUpward;
        Config.Settings["showOvertakingAlert"] = ShowOvertakingAlert;
        Config.Settings["showStartSequence"] = ShowStartSequence;
        Config.Settings["showCheckeredFlag"] = ShowCheckeredFlag;
        Config.Settings["showPaceFlags"] = ShowPaceFlags;
        Config.Settings["showMyIncidents"] = ShowMyIncidents;
        Config.Settings["showRaceStart"] = ShowRaceStart;
        Config.Settings["detectionAhead"] = DetectionAheadSeconds;
        Config.Settings["detectionBehind"] = DetectionBehindSeconds;
    }

    private void InitializeWidget()
    {
        Width = WIDGET_WIDTH;
        Height = (ROW_HEIGHT + ROW_GAP) * MAX_VISIBLE_ROWS + PADDING * 2;

        _canvas = new Canvas
        {
            Width = WIDGET_WIDTH,
            Height = Height,
        };

        _backgroundBorder = new Border
        {
            Width = WIDGET_WIDTH,
            Height = Height,
            CornerRadius = new CornerRadius(BORDER_RADIUS),
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), // transparent when empty
        };

        _feedStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(PADDING),
        };

        _backgroundBorder.Child = _feedStack;
        _canvas.Children.Add(_backgroundBorder);

        // Persistent drag handle — always visible so user can reposition when feed is empty
        _dragHandle = new Border
        {
            Width = 40,
            Height = 14,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromArgb(90, 100, 100, 100)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.SizeAll,
            ToolTip = "Drag to move",
            Child = new TextBlock
            {
                Text = "⋮⋮",
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromArgb(140, 200, 200, 200)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Canvas.SetLeft(_dragHandle, (WIDGET_WIDTH - 40) / 2);
        Canvas.SetTop(_dragHandle, 0);
        _dragHandle.MouseLeftButtonDown += DragHandle_MouseLeftButtonDown;
        _dragHandle.Visibility = Visibility.Collapsed; // Start hidden — user enables via MRT UI checkbox
        _canvas.Children.Add(_dragHandle);

        Content = _canvas;
    }

    /// <summary>
    /// Allow dragging the widget from the grip handle.
    /// </summary>
    private void DragHandle_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
            Config.X = Left;
            Config.Y = Top;
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// Show or hide the drag handle based on lock state.
    /// When locked (normal mode): drag handle hidden, click-through.
    /// When unlocked (drag mode): drag handle shown, interactive.
    /// </summary>
    public void UpdateDragHandleVisibility(bool showHandle)
    {
        _dragHandle.Visibility = showHandle ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Called by telemetry update loop (~60Hz).
    /// Delegates to detector, then syncs visual rows.
    /// </summary>
    protected override void UpdateUI(TelemetryData data)
    {
        // Sync adjustable detection range to detector
        _detector.DetectionAheadSeconds = DetectionAheadSeconds;
        _detector.DetectionBehindSeconds = DetectionBehindSeconds;
        _detector.EnableSessionAlerts = AppSettings.Instance.ShowSessionAlerts;

        // Track formation phase: suppress Stopped/SlowCar during parade laps
        // and during the race start grace period (detector handles the timer).
        // Once the session is RACING and grace has expired, all events are shown.
        _isFormationPhase = data.SessionState == SESSION_STATE_PARADE_LAPS
            || (data.SessionState == SESSION_STATE_RACING && _detector.IsRaceStartGraceActive);

        // The relative table is computed once per tick on the telemetry thread and
        // published on the frame. This widget used to run a second, private
        // RelativeCalculator here — duplicating the whole 64-car pass on the UI
        // thread, with per-car state that could disagree with the Relative widget's.
        _detector.Update(data, data.Relatives);

        SyncFeedVisuals();
    }

    // Reusable collections to avoid per-frame heap allocations
    private readonly List<NearbyEvent> _sortedBuffer = new(16);
    private readonly List<long> _removeBuffer = new(16);
    private readonly HashSet<long> _activeIdBuffer = new(16);
    // Per-car dedup: track best event per CarIdx to avoid GroupBy/LINQ
    private readonly Dictionary<int, NearbyEvent> _dedupBuffer = new(16);
    // Track previous sorted order to skip redundant Children.Clear()/re-add
    private readonly List<long> _prevSortedIds = new(16);

    private void SyncFeedVisuals()
    {
        _blinkFrame++;
        var events = _detector.ActiveEvents;

        // ── FILTER + DEDUP without LINQ (zero allocation) ────
        _sortedBuffer.Clear();
        _dedupBuffer.Clear();

        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];

            // Apply per-type toggle filters (inlined)
            if (!ShowOvertakingAlert && e.EventType == NearbyEventType.OvertakingImminent) continue;
            if (!ShowStartSequence && e.EventType == NearbyEventType.StartSequence) continue;
            if (!ShowCheckeredFlag && e.EventType == NearbyEventType.CheckeredFlag) continue;
            if (!ShowMyIncidents && e.EventType == NearbyEventType.IncidentGained) continue;
            if (!ShowRaceStart && e.EventType is (NearbyEventType.ReactionTime or NearbyEventType.JumpStart)) continue;
            if (!ShowPaceFlags && (e.EventType == NearbyEventType.SafetyCar
                || e.EventType == NearbyEventType.PaceEndOfLine
                || e.EventType == NearbyEventType.PaceFreePass
                || e.EventType == NearbyEventType.PaceWaveAround)) continue;
            if (_isFormationPhase && (e.EventType == NearbyEventType.Stopped
                || e.EventType == NearbyEventType.SlowCar)) continue;

            // Per-car dedup: session-level events (CarIdx < 0) go straight through
            if (e.CarIdx < 0)
            {
                _sortedBuffer.Add(e);
            }
            else
            {
                // Keep highest severity / newest per driver
                if (_dedupBuffer.TryGetValue(e.CarIdx, out var existing))
                {
                    if (e.Severity > existing.Severity ||
                        (e.Severity == existing.Severity && e.CreatedAt > existing.CreatedAt))
                    {
                        _dedupBuffer[e.CarIdx] = e;
                    }
                }
                else
                {
                    _dedupBuffer[e.CarIdx] = e;
                }
            }
        }

        // Add deduped per-car events
        foreach (var kvp in _dedupBuffer)
            _sortedBuffer.Add(kvp.Value);

        // Sort: severity desc, then newest first (in-place, no allocation)
        _sortedBuffer.Sort((a, b) =>
        {
            int cmp = b.Severity.CompareTo(a.Severity);
            return cmp != 0 ? cmp : b.CreatedAt.CompareTo(a.CreatedAt);
        });

        // Trim to max visible
        if (_sortedBuffer.Count > MaxVisibleEvents)
            _sortedBuffer.RemoveRange(MaxVisibleEvents, _sortedBuffer.Count - MaxVisibleEvents);

        if (GrowUpward) _sortedBuffer.Reverse();

        // Show/hide background based on whether there are events
        _backgroundBorder.Background = _sortedBuffer.Count > 0
            ? BrushCache.Get(_bgAlpha, 18, 18, 18)
            : BrushCache.Get(0, 0, 0, 0);

        // Track which event IDs are still active (reuse buffer)
        _activeIdBuffer.Clear();
        for (int i = 0; i < _sortedBuffer.Count; i++)
            _activeIdBuffer.Add(_sortedBuffer[i].Id);

        // Remove rows for expired events (with fade-out)
        _removeBuffer.Clear();
        foreach (var id in _eventRows.Keys)
        {
            if (!_activeIdBuffer.Contains(id))
                _removeBuffer.Add(id);
        }
        for (int i = 0; i < _removeBuffer.Count; i++)
        {
            var id = _removeBuffer[i];
            if (_eventRows.TryGetValue(id, out var row))
            {
                FadeOutAndRemove(row);
                _eventRows.Remove(id);
                _knownEventIds.Remove(id);
            }
        }

        // Check if sorted order changed — skip Children rebuild if identical
        bool orderChanged = _sortedBuffer.Count != _prevSortedIds.Count;
        if (!orderChanged)
        {
            for (int i = 0; i < _sortedBuffer.Count; i++)
            {
                if (_sortedBuffer[i].Id != _prevSortedIds[i])
                {
                    orderChanged = true;
                    break;
                }
            }
        }

        if (orderChanged)
        {
            // Rebuild stack in sorted order (only when order actually changes)
            _feedStack.Children.Clear();
            _prevSortedIds.Clear();
            foreach (var evt in _sortedBuffer)
            {
                _prevSortedIds.Add(evt.Id);
                if (_eventRows.TryGetValue(evt.Id, out var existing))
                {
                    UpdateRowContent(existing, evt);
                    _feedStack.Children.Add(existing);
                }
                else
                {
                    var row = CreateEventRow(evt);
                    _eventRows[evt.Id] = row;
                    _knownEventIds.Add(evt.Id);
                    _feedStack.Children.Add(row);
                    FadeIn(row);
                }
            }
        }
        else
        {
            // Order unchanged — just update content without layout invalidation
            foreach (var evt in _sortedBuffer)
            {
                if (_eventRows.TryGetValue(evt.Id, out var existing))
                    UpdateRowContent(existing, evt);
            }
        }

        // Resize widget height dynamically
        int visibleCount = Math.Max(1, _sortedBuffer.Count);
        double feedHeight = (ROW_HEIGHT + ROW_GAP) * visibleCount + PADDING * 2;
        double handleSpace = _dragHandle.Visibility == Visibility.Visible ? 16 : 0;
        double targetHeight = feedHeight + handleSpace;
        Height = targetHeight;
        _canvas.Height = targetHeight;
        _backgroundBorder.Height = feedHeight;

        // Position drag handle at bottom of feed
        Canvas.SetTop(_dragHandle, feedHeight);
    }

    private Border CreateEventRow(NearbyEvent evt)
    {
        var eventColor = GetEventTypeColor(evt);
        var eventBrush = BrushCache.Get(eventColor);

        // Black flag: special black background with white text
        bool isBlackFlag = evt.EventType == NearbyEventType.BlackFlag;

        var row = new Border
        {
            Height = ROW_HEIGHT,
            Margin = new Thickness(0, 0, 0, ROW_GAP),
            CornerRadius = new CornerRadius(3),
            Background = isBlackFlag
                ? BrushCache.Get(220, 10, 10, 10)
                : BrushCache.Get(Color.FromArgb(40, eventColor.R, eventColor.G, eventColor.B)),
            BorderBrush = isBlackFlag
                ? BrushCache.Get(160, 80, 80, 80)
                : BrushCache.Get(Color.FromArgb(80, eventColor.R, eventColor.G, eventColor.B)),
            BorderThickness = new Thickness(STRIPE_WIDTH, 0, 0, 0),
            Opacity = 0, // start invisible for fade-in
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left: event icon + text + driver
        var leftStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 4, 0),
        };

        // Direction arrow (live-updated in UpdateRowContent as interval sign changes)
        if (ShowDirection)
        {
            var arrow = new TextBlock
            {
                Text = evt.IsAhead ? "▲" : "▼",
                FontSize = 8,
                Foreground = evt.IsAhead ? BrushCache.Get(COLOR_INFO) : BrushCache.Get(COLOR_WARNING),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0),
                Tag = "arrowText",
            };
            leftStack.Children.Add(arrow);
        }

        // Event text
        var eventText = new TextBlock
        {
            Text = evt.DisplayText,
            FontSize = 10,
            FontWeight = evt.Severity >= NearbyEventSeverity.Danger ? FontWeights.Bold : FontWeights.Normal,
            Foreground = eventBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
        };
        eventText.Tag = "eventText"; // for updates
        leftStack.Children.Add(eventText);

        // Driver name (truncated)
        var driverText = new TextBlock
        {
            Text = FormatDriverName(evt.DriverName, evt.CarNumber),
            FontSize = 9,
            Foreground = BRUSH_TEXT,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 90,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        driverText.Tag = "driverText";
        leftStack.Children.Add(driverText);

        Grid.SetColumn(leftStack, 0);
        grid.Children.Add(leftStack);

        // Right: interval
        if (ShowInterval)
        {
            var intervalText = new TextBlock
            {
                Text = FormatInterval(evt.IntervalToPlayer),
                FontSize = 9,
                Foreground = BRUSH_MUTED,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            intervalText.Tag = "intervalText";
            Grid.SetColumn(intervalText, 1);
            grid.Children.Add(intervalText);
        }

        row.Child = grid;
        return row;
    }

    private void UpdateRowContent(Border row, NearbyEvent evt)
    {
        if (row.Child is not Grid grid) return;

        // Determine if text should blink this frame
        bool blinkVisible = (_blinkFrame / BLINK_HALF_PERIOD) % 2 == 0;
        bool shouldBlink = ShouldBlinkText(evt);

        // Overtaking imminent: static first 5s, then blink
        if (evt.EventType == NearbyEventType.OvertakingImminent && evt.Age < 5.0)
            shouldBlink = false;

        // CHAOS ZONE: blink entire row background between orange and red
        bool isChaos = evt.EventType == NearbyEventType.LocalYellow && evt.CarIdx == -2;
        if (isChaos)
        {
            var phase = blinkVisible ? COLOR_CHAOS_ORANGE : COLOR_CHAOS_RED;
            row.Background = BrushCache.Get(Color.FromArgb(80, phase.R, phase.G, phase.B));
            row.BorderBrush = BrushCache.Get(Color.FromArgb(180, phase.R, phase.G, phase.B));
        }

        // Update interval text, direction arrow, and apply text blink
        foreach (UIElement child in grid.Children)
        {
            if (child is TextBlock tb && tb.Tag as string == "intervalText")
            {
                tb.Text = FormatInterval(evt.IntervalToPlayer);
            }
            if (child is StackPanel sp)
            {
                foreach (UIElement spChild in sp.Children)
                {
                    if (spChild is TextBlock etb)
                    {
                        if (etb.Tag as string == "eventText")
                        {
                            // Blink effect: toggle opacity of the event text only (not bar/border)
                            if (shouldBlink)
                                etb.Opacity = blinkVisible ? 1.0 : 0.25;
                            else
                                etb.Opacity = 1.0;
                        }
                        else if (etb.Tag as string == "arrowText")
                        {
                            // Live-update direction arrow as interval changes (overtakes)
                            bool ahead = evt.IntervalToPlayer > 0;
                            etb.Text = ahead ? "▲" : "▼";
                            etb.Foreground = ahead
                                ? BrushCache.Get(COLOR_INFO)
                                : BrushCache.Get(COLOR_WARNING);
                        }
                    }
                }
            }
        }

        // Ongoing indicator: keep full opacity while condition is active
        if (evt.IsOngoing)
        {
            row.Opacity = 1.0;
            return;
        }

        // Fade opacity based on age (last 25% of duration fades out)
        double age = evt.ClearedAt.HasValue ? evt.AgeSinceCleared : evt.Age;
        double duration = evt.DisplayDuration;
        double ageRatio = age / duration;
        if (ageRatio > 0.75)
        {
            double fadeRatio = (ageRatio - 0.75) / 0.25; // 0..1
            row.Opacity = Math.Max(0.15, 1.0 - fadeRatio * 0.85);
        }
        else
        {
            row.Opacity = 1.0;
        }
    }

    // ── Animation helpers ───────────────────────────────────────────

    private static void FadeIn(Border row)
    {
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(FADE_IN_MS))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        row.BeginAnimation(OpacityProperty, anim);
    }

    private void FadeOutAndRemove(Border row)
    {
        var anim = new DoubleAnimation(row.Opacity, 0, TimeSpan.FromMilliseconds(FADE_OUT_MS))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
        };
        anim.Completed += (_, _) =>
        {
            _feedStack.Children.Remove(row);
        };
        row.BeginAnimation(OpacityProperty, anim);
    }

    // ── Formatting helpers ──────────────────────────────────────────

    private static string FormatDriverName(string name, string carNum)
    {
        // "J.Smith #44" format
        if (string.IsNullOrWhiteSpace(name)) return $"#{carNum}";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string formatted = parts.Length >= 2
            ? $"{parts[0][0]}.{parts[^1]}"
            : name;

        if (formatted.Length > 10) formatted = formatted[..10];
        return string.IsNullOrEmpty(carNum) ? formatted : $"{formatted} #{carNum}";
    }

    private static string FormatInterval(float interval)
    {
        string sign = interval >= 0 ? "+" : "";
        return $"{sign}{interval.ToString("F1", CultureInfo.InvariantCulture)}s";
    }

    private static Color GetSeverityColor(NearbyEventSeverity severity) => severity switch
    {
        NearbyEventSeverity.Info => COLOR_INFO,
        NearbyEventSeverity.Warning => COLOR_WARNING,
        NearbyEventSeverity.Danger => COLOR_DANGER,
        NearbyEventSeverity.Critical => COLOR_CRITICAL,
        _ => COLOR_INFO,
    };

    /// <summary>
    /// Get event-specific color for richer visual differentiation.
    /// Falls back to severity color for unknown types.
    /// </summary>
    private static Color GetEventTypeColor(NearbyEvent evt)
    {
        // Incident escalation overrides individual event colors
        if (evt.IsIncident)
            return evt.IsAhead ? COLOR_INCIDENT : COLOR_DANGER;

        // CHAOS ZONE (pack density) — uses orange as base, blinks to red in UpdateRowContent
        if (evt.EventType == NearbyEventType.LocalYellow && evt.CarIdx == -2)
            return COLOR_CHAOS_ORANGE;

        return evt.EventType switch
        {
        NearbyEventType.OffTrack => COLOR_OFF_TRACK,
        NearbyEventType.Collision => COLOR_COLLISION,
        NearbyEventType.SlowCar => COLOR_SLOW_CAR,
        NearbyEventType.Stopped => COLOR_STOPPED,
        NearbyEventType.Pitting => COLOR_PITTING,
        NearbyEventType.InBox => COLOR_IN_BOX,
        NearbyEventType.PitExit => COLOR_PIT_EXIT,
        NearbyEventType.MeatballFlag => COLOR_MEATBALL,
        NearbyEventType.BlackFlag => COLOR_BLACK_FLAG,
        NearbyEventType.Towed => COLOR_TOWED,
        NearbyEventType.LocalYellow => COLOR_LOCAL_YELLOW,
        NearbyEventType.Spin => COLOR_SPIN,
        NearbyEventType.OvertakingImminent => COLOR_OVERTAKING,
        NearbyEventType.Disqualified => COLOR_DISQUALIFIED,
        NearbyEventType.BlueFlagged => COLOR_BLUE_FLAG,
        NearbyEventType.SafetyCar => COLOR_SAFETY_CAR,
        NearbyEventType.StartSequence => COLOR_START_SEQ,
        NearbyEventType.CheckeredFlag => COLOR_CHECKERED,
        NearbyEventType.WhiteFlag => COLOR_WHITE_FLAG,
        NearbyEventType.RedFlag => COLOR_RED_FLAG,
        NearbyEventType.PaceEndOfLine => COLOR_PACE_FLAG,
        NearbyEventType.PaceFreePass => COLOR_PACE_FLAG,
        NearbyEventType.PaceWaveAround => COLOR_PACE_FLAG,
        NearbyEventType.IncomingFast => COLOR_INCOMING_FAST,

        // Session alerts about the player's own car and conditions
        NearbyEventType.WeatherChange => COLOR_RAIN,
        NearbyEventType.DeclaredWet => COLOR_RAIN,
        NearbyEventType.EngineWarning => COLOR_ENGINE,
        NearbyEventType.PoorConnection => COLOR_NETWORK,
        NearbyEventType.FfbClipping => COLOR_FFB,
        NearbyEventType.LowTireSets => COLOR_TYRES,
        NearbyEventType.IncidentGained => COLOR_MY_INCIDENT,
        NearbyEventType.PitLaneStatus => COLOR_PIT_LANE,
        NearbyEventType.ReactionTime => COLOR_REACTION,
        NearbyEventType.JumpStart => COLOR_JUMP_START,

        _ => GetSeverityColor(evt.Severity),
    };
    }

    /// <summary>
    /// Per-type blink rules:
    ///   BLINK: OffTrack, Collision, Stopped, PitExit, MeatballFlag, LocalYellow, OvertakingImminent (after 5s),
    ///          RedFlag, CheckeredFlag, StartSequence, BlueFlagged, SafetyCar
    ///   STATIC: Pitting, SlowCar, Towed, BlackFlag, Spin, Disqualified, PaceEndOfLine, PaceFreePass, PaceWaveAround
    /// </summary>
    private static bool ShouldBlinkText(NearbyEvent evt) =>
        evt.IsIncident  // Incidents always blink
        || evt.EventType is NearbyEventType.OffTrack
            or NearbyEventType.Collision
            or NearbyEventType.Stopped
            or NearbyEventType.PitExit
            or NearbyEventType.MeatballFlag
            or NearbyEventType.LocalYellow
            or NearbyEventType.OvertakingImminent
            or NearbyEventType.RedFlag
            or NearbyEventType.CheckeredFlag
            or NearbyEventType.WhiteFlag
            or NearbyEventType.StartSequence
            or NearbyEventType.BlueFlagged
            or NearbyEventType.SafetyCar
            or NearbyEventType.IncomingFast
            // Declared-wet changes what tires are legal, and an engine fault ends
            // races — both worth blinking. Weather trend, connection, FFB and tire
            // sets are advisory and stay static.
            or NearbyEventType.DeclaredWet
            or NearbyEventType.EngineWarning;
}
