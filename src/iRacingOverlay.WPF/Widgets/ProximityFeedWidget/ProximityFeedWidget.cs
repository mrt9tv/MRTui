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
/// Proximity Feed Widget — animated event notifications for nearby cars (±15s).
/// Lightweight vertical feed showing collisions, off-tracks, pitting, stopped cars, flags.
/// Events slide in, display briefly, then fade out. Most severe events at top.
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

    // Severity colors
    private static readonly Color COLOR_INFO = Color.FromRgb(0, 150, 180);     // teal
    private static readonly Color COLOR_WARNING = Color.FromRgb(255, 180, 0);  // amber
    private static readonly Color COLOR_DANGER = Color.FromRgb(255, 60, 60);   // red
    private static readonly Color COLOR_CRITICAL = Color.FromRgb(255, 20, 20); // bright red

    private static readonly SolidColorBrush BRUSH_TEXT = new(COLOR_TEXT);
    private static readonly SolidColorBrush BRUSH_MUTED = new(COLOR_MUTED);

    #endregion

    #region State

    private Canvas _canvas = null!;
    private Border _backgroundBorder = null!;
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
    }

    public void SaveSettings()
    {
        Config.Settings["maxVisibleEvents"] = MaxVisibleEvents;
        Config.Settings["showDirection"] = ShowDirection;
        Config.Settings["showInterval"] = ShowInterval;
        Config.Settings["growUpward"] = GrowUpward;
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
        Content = _canvas;
    }

    /// <summary>
    /// Called by telemetry update loop (~60Hz).
    /// Delegates to detector, then syncs visual rows.
    /// </summary>
    protected override void UpdateUI(TelemetryData data)
    {
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
        var events = _detector.ActiveEvents;

        // Sort: severity desc, then newest first
        var sorted = events
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
        double targetHeight = (ROW_HEIGHT + ROW_GAP) * visibleCount + PADDING * 2;
        Height = targetHeight;
        _canvas.Height = targetHeight;
        _backgroundBorder.Height = targetHeight;
    }

    private Border CreateEventRow(NearbyEvent evt)
    {
        var severityColor = GetSeverityColor(evt.Severity);
        var severityBrush = new SolidColorBrush(severityColor);

        var row = new Border
        {
            Height = ROW_HEIGHT,
            Margin = new Thickness(0, 0, 0, ROW_GAP),
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromArgb(40, severityColor.R, severityColor.G, severityColor.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, severityColor.R, severityColor.G, severityColor.B)),
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

        // Direction arrow
        if (ShowDirection)
        {
            var arrow = new TextBlock
            {
                Text = evt.IsAhead ? "▲" : "▼",
                FontSize = 8,
                Foreground = evt.IsAhead ? new SolidColorBrush(COLOR_INFO) : new SolidColorBrush(COLOR_WARNING),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0),
            };
            leftStack.Children.Add(arrow);
        }

        // Event text
        var eventText = new TextBlock
        {
            Text = evt.DisplayText,
            FontSize = 10,
            FontWeight = evt.Severity >= NearbyEventSeverity.Danger ? FontWeights.Bold : FontWeights.Normal,
            Foreground = severityBrush,
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

        // Update interval text if it exists
        foreach (var child in grid.Children)
        {
            if (child is TextBlock tb && tb.Tag as string == "intervalText")
            {
                tb.Text = FormatInterval(evt.IntervalToPlayer);
            }
        }

        // Fade opacity based on age (last 25% of duration fades out)
        double ageRatio = evt.Age / evt.DisplayDuration;
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
        return $"{sign}{interval:F1}s";
    }

    private static Color GetSeverityColor(NearbyEventSeverity severity) => severity switch
    {
        NearbyEventSeverity.Info => COLOR_INFO,
        NearbyEventSeverity.Warning => COLOR_WARNING,
        NearbyEventSeverity.Danger => COLOR_DANGER,
        NearbyEventSeverity.Critical => COLOR_CRITICAL,
        _ => COLOR_INFO,
    };
}
