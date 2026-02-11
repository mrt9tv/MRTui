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

namespace iRacingOverlay.WPF.Widgets.ProximityFeedWidget;

/// <summary>
/// Proximity Feed Widget — animated event notifications for nearby cars (14s ahead, 7s behind).
/// Lightweight vertical feed showing collisions, off-tracks, pitting, stopped cars, flags.
/// Events slide in, display briefly, then fade out. Most severe events at top.
/// Includes a persistent drag handle for repositioning even when feed is empty.
///
/// MRT theme: dark translucent background, severity-colored left stripe, compact text.
/// </summary>
public class ProximityFeedWidget : WidgetBase
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
    private const double FADE_IN_MS = 250;
    private const double FADE_OUT_MS = 400;

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
    private static readonly Color COLOR_BLACK_FLAG = Color.FromRgb(180, 0, 180);     // magenta
    private static readonly Color COLOR_TOWED = Color.FromRgb(180, 80, 220);         // purple
    private static readonly Color COLOR_LOCAL_YELLOW = Color.FromRgb(255, 230, 0);   // bright yellow
    private static readonly Color COLOR_SPIN = Color.FromRgb(220, 120, 0);           // deep orange
    private static readonly Color COLOR_OVERTAKING = Color.FromRgb(255, 140, 0);     // orange for higher-class overtake imminent
    private static readonly Color COLOR_DISQUALIFIED = Color.FromRgb(140, 0, 0);     // dark red
    private static readonly Color COLOR_BLUE_FLAG = Color.FromRgb(0, 100, 255);      // blue
    private static readonly Color COLOR_SAFETY_CAR = Color.FromRgb(255, 230, 0);     // bright yellow
    private static readonly Color COLOR_START_SEQ = Color.FromRgb(0, 200, 80);       // green (GO!)
    private static readonly Color COLOR_CHECKERED = Color.FromRgb(255, 255, 255);    // white
    private static readonly Color COLOR_RED_FLAG = Color.FromRgb(255, 0, 0);         // red
    private static readonly Color COLOR_PACE_FLAG = Color.FromRgb(200, 200, 80);     // warm yellow-green

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
    private readonly RelativeCalculator _relativeCalculator = new();
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

    /// <summary>Detection range ahead of the player in seconds (1-30, default 12).</summary>
    public float DetectionAheadSeconds { get; set; } = 12.0f;

    /// <summary>Detection range behind the player in seconds (1-15, default 6).</summary>
    public float DetectionBehindSeconds { get; set; } = 6.0f;

    /// <summary>Whether the player has crossed S/F at least once (suppresses feed before then).</summary>
    private bool _playerHasCrossedSF;
    private int _prevLapsCompleted = -1;

    #endregion

    public override WidgetType WidgetType => WidgetType.ProximityFeed;

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
        // Track S/F crossing: used to suppress formation-lap noise (stopped/slow)
        // while still allowing meaningful events (off-track, collision, session events)
        // Sync adjustable detection range to detector
        _detector.DetectionAheadSeconds = DetectionAheadSeconds;
        _detector.DetectionBehindSeconds = DetectionBehindSeconds;

        if (!_playerHasCrossedSF)
        {
            if (data.LapsCompleted >= 1 && _prevLapsCompleted >= 0 && data.LapsCompleted > _prevLapsCompleted)
                _playerHasCrossedSF = true;
            _prevLapsCompleted = data.LapsCompleted;
        }

        // Get relative entries from the calculator (already computed this frame)
        IReadOnlyList<RelativeEntry>? relatives = null;
        try
        {
            // The RelativeCalculator's last result is consumed via the widget system.
            // We pass null if not available — detector handles gracefully.
            relatives = _relativeCalculator.Calculate(data, int.MaxValue, int.MaxValue);
        }
        catch { /* Calculator may fail if arrays not yet populated */ }

        _detector.Update(data, relatives);

        SyncFeedVisuals();
    }

    private void SyncFeedVisuals()
    {
        _blinkFrame++;
        var events = _detector.ActiveEvents;

        // Filter: never show InBox in proximity feed; respect per-type toggles
        var filtered = events.Where(e => e.EventType != NearbyEventType.InBox);
        if (!ShowOvertakingAlert)
            filtered = filtered.Where(e => e.EventType != NearbyEventType.OvertakingImminent);
        if (!ShowStartSequence)
            filtered = filtered.Where(e => e.EventType != NearbyEventType.StartSequence);
        if (!ShowCheckeredFlag)
            filtered = filtered.Where(e => e.EventType != NearbyEventType.CheckeredFlag);
        if (!ShowPaceFlags)
            filtered = filtered.Where(e => e.EventType != NearbyEventType.SafetyCar
                && e.EventType != NearbyEventType.PaceEndOfLine
                && e.EventType != NearbyEventType.PaceFreePass
                && e.EventType != NearbyEventType.PaceWaveAround);

        // Before first S/F crossing: suppress stopped/slow noise (all cars are slow during formation)
        if (!_playerHasCrossedSF)
            filtered = filtered.Where(e => e.EventType != NearbyEventType.Stopped
                && e.EventType != NearbyEventType.SlowCar);

        // Sort: severity desc, then newest first
        var sorted = filtered
            .OrderByDescending(e => e.Severity)
            .ThenByDescending(e => e.CreatedAt)
            .Take(MaxVisibleEvents)
            .ToList();

        if (GrowUpward) sorted.Reverse();

        // Show/hide background based on whether there are events
        _backgroundBorder.Background = sorted.Count > 0
            ? new SolidColorBrush(COLOR_BG)
            : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        // Track which event IDs are still active
        var activeIds = new HashSet<long>(sorted.Select(e => e.Id));

        // Remove rows for expired events (with fade-out)
        var toRemove = _eventRows.Keys.Where(id => !activeIds.Contains(id)).ToList();
        foreach (var id in toRemove)
        {
            if (_eventRows.TryGetValue(id, out var row))
            {
                FadeOutAndRemove(row);
                _eventRows.Remove(id);
                _knownEventIds.Remove(id);
            }
        }

        // Rebuild stack in sorted order
        _feedStack.Children.Clear();
        foreach (var evt in sorted)
        {
            if (_eventRows.TryGetValue(evt.Id, out var existing))
            {
                // Update existing row (interval may have changed)
                UpdateRowContent(existing, evt);
                _feedStack.Children.Add(existing);
            }
            else
            {
                // Create new row with fade-in
                var row = CreateEventRow(evt);
                _eventRows[evt.Id] = row;
                _knownEventIds.Add(evt.Id);
                _feedStack.Children.Add(row);
                FadeIn(row);
            }
        }

        // Resize widget height dynamically
        int visibleCount = Math.Max(1, sorted.Count);
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
        var eventBrush = new SolidColorBrush(eventColor);

        var row = new Border
        {
            Height = ROW_HEIGHT,
            Margin = new Thickness(0, 0, 0, ROW_GAP),
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromArgb(40, eventColor.R, eventColor.G, eventColor.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, eventColor.R, eventColor.G, eventColor.B)),
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
                Foreground = evt.IsAhead ? new SolidColorBrush(COLOR_INFO) : new SolidColorBrush(COLOR_WARNING),
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
                                ? new SolidColorBrush(COLOR_INFO)
                                : new SolidColorBrush(COLOR_WARNING);
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
    private static Color GetEventTypeColor(NearbyEvent evt) => evt.EventType switch
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
        NearbyEventType.RedFlag => COLOR_RED_FLAG,
        NearbyEventType.PaceEndOfLine => COLOR_PACE_FLAG,
        NearbyEventType.PaceFreePass => COLOR_PACE_FLAG,
        NearbyEventType.PaceWaveAround => COLOR_PACE_FLAG,
        _ => GetSeverityColor(evt.Severity),
    };

    /// <summary>
    /// Per-type blink rules:
    ///   BLINK: OffTrack, Collision, Stopped, PitExit, MeatballFlag, LocalYellow, OvertakingImminent (after 5s),
    ///          RedFlag, CheckeredFlag, StartSequence, BlueFlagged, SafetyCar
    ///   STATIC: Pitting, SlowCar, Towed, BlackFlag, Spin, Disqualified, PaceEndOfLine, PaceFreePass, PaceWaveAround
    /// </summary>
    private static bool ShouldBlinkText(NearbyEvent evt) =>
        evt.EventType is NearbyEventType.OffTrack
            or NearbyEventType.Collision
            or NearbyEventType.Stopped
            or NearbyEventType.PitExit
            or NearbyEventType.MeatballFlag
            or NearbyEventType.LocalYellow
            or NearbyEventType.OvertakingImminent
            or NearbyEventType.RedFlag
            or NearbyEventType.CheckeredFlag
            or NearbyEventType.StartSequence
            or NearbyEventType.BlueFlagged
            or NearbyEventType.SafetyCar;
}
