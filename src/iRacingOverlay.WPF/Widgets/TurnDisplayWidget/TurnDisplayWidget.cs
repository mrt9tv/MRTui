using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.TurnDisplayWidget;

/// <summary>
/// Turn Display Widget - Vertical layout showing next and last turn.
/// Compact vertical bar: NEXT (teal) → LAST (orange)
/// </summary>
public class TurnDisplayWidget : WidgetBase
{
    #region UI Elements

    private Canvas _mainCanvas = null!;
    private Grid _contentGrid = null!;
    
    // Next turn (top, teal)
    private Border _nextRow = null!;
    private TextBlock _nextTurnNumber = null!;
    private TextBlock _nextTurnName = null!;
    
    // Last turn (bottom, orange)
    private Border _lastRow = null!;
    private TextBlock _lastTurnNumber = null!;
    private TextBlock _lastTurnName = null!;
    
    private Border _backgroundBorder = null!;

    #endregion

    #region Colors (MRT One Theme)

    private static readonly Color COLOR_TEAL = Color.FromRgb(0, 128, 128);         // #008080
    private static readonly Color COLOR_ORANGE = Color.FromRgb(255, 128, 0);       // #FF8000
    private static readonly Color COLOR_DARK_BG = Color.FromArgb(250, 18, 18, 18); // #121212FA — higher opacity
    private static readonly Color COLOR_TEXT = Color.FromRgb(240, 240, 240);       // #F0F0F0
    private static readonly Color COLOR_MUTED = Color.FromRgb(136, 136, 136);      // #888888

    #endregion

    #region Sizing Constants

    private const double WIDGET_WIDTH = 180;
    private const double WIDGET_WIDTH_COMPACT = 70;
    private const double WIDGET_HEIGHT = 110;
    private const double ROW_HEIGHT = 44;
    private const double PADDING = 6;
    private const double BORDER_RADIUS = 6;
    private const double BORDER_THICKNESS = 2;
    private const double GLOW_BLUR_RADIUS = 8;

    #endregion

    #region Settings

    /// <summary>Whether to show turn names (true) or just turn numbers (false)</summary>
    public bool ShowTurnName { get; set; } = true;

    /// <summary>Whether to animate border color based on turn progress</summary>
    public bool AnimateBorder { get; set; } = false;

    #endregion

    public override WidgetType WidgetType => WidgetType.TurnDisplay;

    public TurnDisplayWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        Title = "Turn Display";
        Width = WIDGET_WIDTH;
        Height = WIDGET_HEIGHT;

        LoadSettings();
        InitializeWidget();
    }

    private void LoadSettings()
    {
        if (Config?.Settings == null) return;

        if (Config.Settings.TryGetValue("showTurnName", out var v1))
        {
            if (v1 is JsonElement je1) ShowTurnName = je1.ValueKind == JsonValueKind.True;
            else if (v1 is bool b1) ShowTurnName = b1;
        }

        if (Config.Settings.TryGetValue("animateBorder", out var v2))
        {
            if (v2 is JsonElement je2) AnimateBorder = je2.ValueKind == JsonValueKind.True;
            else if (v2 is bool b2) AnimateBorder = b2;
        }
    }

    public void SaveSettings()
    {
        if (Config == null) return;
        Config.Settings["showTurnName"] = ShowTurnName;
        Config.Settings["animateBorder"] = AnimateBorder;
    }

    private void InitializeWidget()
    {
        _mainCanvas = new Canvas
        {
            Width = WIDGET_WIDTH,
            Height = WIDGET_HEIGHT,
            Background = Brushes.Transparent
        };

        _backgroundBorder = new Border
        {
            Width = WIDGET_WIDTH,
            Height = WIDGET_HEIGHT,
            Background = new SolidColorBrush(COLOR_DARK_BG),
            BorderBrush = new SolidColorBrush(COLOR_TEAL),
            BorderThickness = new Thickness(BORDER_THICKNESS),
            CornerRadius = new CornerRadius(BORDER_RADIUS),
            Effect = new DropShadowEffect
            {
                Color = COLOR_TEAL,
                BlurRadius = GLOW_BLUR_RADIUS,
                ShadowDepth = 0,
                Opacity = 0.6
            }
        };

        // Vertical content grid: 2 rows with separator
        _contentGrid = new Grid
        {
            Margin = new Thickness(PADDING)
        };

        _contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) }); // sep
        _contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // NEXT TURN (top, teal)
        _nextRow = CreateTurnRow(out _nextTurnNumber, out _nextTurnName, "NEXT", COLOR_TEAL);
        Grid.SetRow(_nextRow, 0);
        _contentGrid.Children.Add(_nextRow);

        // Separator
        var sep = CreateSeparator();
        Grid.SetRow(sep, 1);
        _contentGrid.Children.Add(sep);

        // LAST TURN (bottom, orange)
        _lastRow = CreateTurnRow(out _lastTurnNumber, out _lastTurnName, "LAST", COLOR_ORANGE);
        Grid.SetRow(_lastRow, 2);
        _contentGrid.Children.Add(_lastRow);

        _backgroundBorder.Child = _contentGrid;
        _mainCanvas.Children.Add(_backgroundBorder);
        Content = _mainCanvas;
    }

    private Border CreateTurnRow(out TextBlock numberBlock, out TextBlock nameBlock, string label, Color accentColor)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42, GridUnitType.Pixel) }); // wider for T23
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Label + Number stack (left) — left-aligned
        var leftStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 7,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(COLOR_MUTED),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        leftStack.Children.Add(labelBlock);

        numberBlock = new TextBlock
        {
            Text = "—",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(accentColor),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, -2, 0, 0) // tighten spacing between NEXT/LAST and T#
        };
        leftStack.Children.Add(numberBlock);

        Grid.SetColumn(leftStack, 0);
        grid.Children.Add(leftStack);

        // Turn name (right) — vertically centered with the label+number stack
        nameBlock = new TextBlock
        {
            Text = "",
            FontSize = 11,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(COLOR_TEXT),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(4, 0, 0, 0),
            MaxWidth = 120,
            Visibility = ShowTurnName ? Visibility.Visible : Visibility.Collapsed
        };
        Grid.SetColumn(nameBlock, 1);
        grid.Children.Add(nameBlock);

        border.Child = grid;
        return border;
    }

    private static Border CreateSeparator()
    {
        return new Border
        {
            Height = 1,
            Background = new SolidColorBrush(COLOR_MUTED),
            Opacity = 0.25,
            Margin = new Thickness(4, 2, 4, 2)
        };
    }

    protected override void UpdateUI(TelemetryData data)
    {
        // Update turn name visibility and resize widget
        bool showNames = ShowTurnName;
        _nextTurnName.Visibility = showNames ? Visibility.Visible : Visibility.Collapsed;
        _lastTurnName.Visibility = showNames ? Visibility.Visible : Visibility.Collapsed;

        // Compact width when names are off (just show T# and label)
        double targetWidth = showNames ? WIDGET_WIDTH : WIDGET_WIDTH_COMPACT;
        if (Math.Abs(_mainCanvas.Width - targetWidth) > 1)
        {
            _mainCanvas.Width = targetWidth;
            _backgroundBorder.Width = targetWidth;
            Width = targetWidth;
        }

        // Border animation (optional — off by default)
        if (AnimateBorder && data.IsInTurn && data.TurnNumber > 0)
        {
            var progressColor = InterpolateColor(COLOR_TEAL, COLOR_ORANGE, data.TurnProgress);
            _backgroundBorder.BorderBrush = new SolidColorBrush(progressColor);
            if (_backgroundBorder.Effect is DropShadowEffect glow)
                glow.Color = progressColor;
        }
        else
        {
            _backgroundBorder.BorderBrush = new SolidColorBrush(COLOR_TEAL);
            if (_backgroundBorder.Effect is DropShadowEffect glow)
                glow.Color = COLOR_TEAL;
        }

        // Next turn
        if (data.NextTurnNumber > 0)
        {
            _nextTurnNumber.Text = $"T{data.NextTurnNumber}";
            _nextTurnName.Text = data.NextTurnName;
        }
        else
        {
            _nextTurnNumber.Text = "—";
            _nextTurnName.Text = "";
        }

        // Last turn
        if (data.LastTurnNumber > 0)
        {
            _lastTurnNumber.Text = $"T{data.LastTurnNumber}";
            _lastTurnName.Text = data.LastTurnName;
        }
        else
        {
            _lastTurnNumber.Text = "—";
            _lastTurnName.Text = "";
        }
    }

    /// <summary>
    /// Interpolate between two colors based on progress (0.0 - 1.0)
    /// </summary>
    private Color InterpolateColor(Color start, Color end, float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        
        byte r = (byte)(start.R + (end.R - start.R) * progress);
        byte g = (byte)(start.G + (end.G - start.G) * progress);
        byte b = (byte)(start.B + (end.B - start.B) * progress);
        
        return Color.FromRgb(r, g, b);
    }
}
