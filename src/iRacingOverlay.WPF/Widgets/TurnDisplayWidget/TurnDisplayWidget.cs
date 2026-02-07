using System;
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
/// Turn Display Widget - Shows current turn number and name
/// Compact horizontal bar design with MRT One color theme (teal + orange)
/// Toggleable turn name display
/// </summary>
public class TurnDisplayWidget : WidgetBase
{
    #region UI Elements

    private Canvas _mainCanvas = null!;
    private Grid _contentGrid = null!;
    private TextBlock _turnNumberText = null!;
    private TextBlock _turnNameText = null!;
    private Border _backgroundBorder = null!;

    #endregion

    #region Colors (MRT One Theme)

    // Primary colors from MRT One widget
    private static readonly Color COLOR_TEAL = Color.FromRgb(0, 128, 128);         // #008080
    private static readonly Color COLOR_ORANGE = Color.FromRgb(255, 128, 0);       // #FF8000
    private static readonly Color COLOR_DARK_BG = Color.FromArgb(230, 18, 18, 18); // #12121299 (90% opacity)
    private static readonly Color COLOR_TEXT = Color.FromRgb(240, 240, 240);       // #F0F0F0

    #endregion

    #region Sizing Constants

    private const double WIDGET_HEIGHT = 40;
    private const double WIDGET_WIDTH_COMPACT = 70;   // "T3" only
    private const double WIDGET_WIDTH_FULL = 200;     // "T3: Eau Rouge"
    private const double PADDING = 8;
    private const double BORDER_RADIUS = 6;
    private const double BORDER_THICKNESS = 2;
    private const double GLOW_BLUR_RADIUS = 8;

    #endregion

    #region Settings

    /// <summary>
    /// Whether to show turn name (true) or just turn number (false)
    /// </summary>
    public bool ShowTurnName { get; set; } = true;

    #endregion

    public override WidgetType WidgetType => WidgetType.TurnDisplay;

    public TurnDisplayWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        Title = "Turn Display";
        Width = ShowTurnName ? WIDGET_WIDTH_FULL : WIDGET_WIDTH_COMPACT;
        Height = WIDGET_HEIGHT;

        InitializeWidget();
    }

    private void InitializeWidget()
    {
        // Main canvas
        _mainCanvas = new Canvas
        {
            Width = Width,
            Height = WIDGET_HEIGHT,
            Background = Brushes.Transparent
        };

        // Background border with rounded corners and glow
        _backgroundBorder = new Border
        {
            Width = Width,
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

        // Content grid (horizontal layout)
        _contentGrid = new Grid
        {
            Width = Width - (PADDING * 2),
            Height = WIDGET_HEIGHT - (PADDING * 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Define columns: Turn number (fixed) | Turn name (auto)
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Turn number text (always visible)
        _turnNumberText = new TextBlock
        {
            Text = "—",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(COLOR_ORANGE),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(_turnNumberText, 0);
        _contentGrid.Children.Add(_turnNumberText);

        // Turn name text (toggleable)
        _turnNameText = new TextBlock
        {
            Text = "",
            FontSize = 14,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(COLOR_TEXT),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Visibility = ShowTurnName ? Visibility.Visible : Visibility.Collapsed
        };
        Grid.SetColumn(_turnNameText, 1);
        _contentGrid.Children.Add(_turnNameText);

        // Place content inside border
        _backgroundBorder.Child = _contentGrid;

        // Add border to canvas
        _mainCanvas.Children.Add(_backgroundBorder);

        // Set canvas as window content
        Content = _mainCanvas;
    }

    protected override void UpdateUI(TelemetryData data)
    {
        // Update turn display based on telemetry
        UpdateTurnDisplay(data);
    }

    private void UpdateTurnDisplay(TelemetryData data)
    {
        // Check if in a turn
        if (data.IsInTurn && data.TurnNumber > 0)
        {
            // Format turn number as "T3"
            _turnNumberText.Text = $"T{data.TurnNumber}";
            _turnNumberText.Foreground = new SolidColorBrush(COLOR_ORANGE);

            // Show turn name if enabled and available
            if (ShowTurnName && !string.IsNullOrEmpty(data.TurnName) && data.TurnName != "-")
            {
                _turnNameText.Text = data.TurnName;
                _turnNameText.Visibility = Visibility.Visible;
                
                // Adjust widget width for full mode
                if (Width != WIDGET_WIDTH_FULL)
                {
                    Width = WIDGET_WIDTH_FULL;
                    _mainCanvas.Width = WIDGET_WIDTH_FULL;
                    _backgroundBorder.Width = WIDGET_WIDTH_FULL;
                    _contentGrid.Width = WIDGET_WIDTH_FULL - (PADDING * 2);
                }
            }
            else
            {
                // Hide name, show compact mode
                _turnNameText.Visibility = Visibility.Collapsed;
                if (Width != WIDGET_WIDTH_COMPACT)
                {
                    Width = WIDGET_WIDTH_COMPACT;
                    _mainCanvas.Width = WIDGET_WIDTH_COMPACT;
                    _backgroundBorder.Width = WIDGET_WIDTH_COMPACT;
                    _contentGrid.Width = WIDGET_WIDTH_COMPACT - (PADDING * 2);
                }
            }

            // Animate border color based on turn progress (teal → orange)
            var progressColor = InterpolateColor(COLOR_TEAL, COLOR_ORANGE, data.TurnProgress);
            _backgroundBorder.BorderBrush = new SolidColorBrush(progressColor);
            
            // Update glow effect
            if (_backgroundBorder.Effect is DropShadowEffect glow)
            {
                glow.Color = progressColor;
            }
        }
        else
        {
            // Not in a turn - show dash
            _turnNumberText.Text = "—";
            _turnNumberText.Foreground = new SolidColorBrush(COLOR_TEXT);
            _turnNameText.Text = "";
            _turnNameText.Visibility = Visibility.Collapsed;
            _backgroundBorder.BorderBrush = new SolidColorBrush(COLOR_TEAL);
            
            // Restore glow
            if (_backgroundBorder.Effect is DropShadowEffect glow)
            {
                glow.Color = COLOR_TEAL;
            }

            // Compact mode when not in turn
            if (Width != WIDGET_WIDTH_COMPACT)
            {
                Width = WIDGET_WIDTH_COMPACT;
                _mainCanvas.Width = WIDGET_WIDTH_COMPACT;
                _backgroundBorder.Width = WIDGET_WIDTH_COMPACT;
                _contentGrid.Width = WIDGET_WIDTH_COMPACT - (PADDING * 2);
            }
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

    /// <summary>
    /// Toggle turn name display on/off
    /// </summary>
    public void ToggleTurnName()
    {
        ShowTurnName = !ShowTurnName;
        _turnNameText.Visibility = ShowTurnName ? Visibility.Visible : Visibility.Collapsed;
        
        // Force refresh with current telemetry
        if (_lastTelemetryData != null)
        {
            UpdateTurnDisplay(_lastTelemetryData);
        }
    }
}
