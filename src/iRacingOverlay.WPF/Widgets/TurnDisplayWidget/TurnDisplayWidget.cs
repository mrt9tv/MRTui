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
/// Turn Display Widget - Shows last completed turn, current turn, and next upcoming turn
/// Fixed-width horizontal bar design with MRT One color theme (teal + orange)
/// Format: "T14: Parabolica | T1: Rettifilo | T2: Roggia"
/// </summary>
public class TurnDisplayWidget : WidgetBase
{
    #region UI Elements

    private Canvas _mainCanvas = null!;
    private Grid _contentGrid = null!;
    
    // Last turn (completed)
    private StackPanel _lastTurnPanel = null!;
    private TextBlock _lastTurnLabel = null!;
    private TextBlock _lastTurnNumber = null!;
    private TextBlock _lastTurnName = null!;
    
    // Current turn (in progress)
    private StackPanel _currentTurnPanel = null!;
    private TextBlock _currentTurnLabel = null!;
    private TextBlock _currentTurnNumber = null!;
    private TextBlock _currentTurnName = null!;
    
    // Next turn (upcoming)
    private StackPanel _nextTurnPanel = null!;
    private TextBlock _nextTurnLabel = null!;
    private TextBlock _nextTurnNumber = null!;
    private TextBlock _nextTurnName = null!;
    
    private Border _backgroundBorder = null!;

    #endregion

    #region Colors (MRT One Theme)

    // Primary colors from MRT One widget
    private static readonly Color COLOR_TEAL = Color.FromRgb(0, 128, 128);         // #008080
    private static readonly Color COLOR_ORANGE = Color.FromRgb(255, 128, 0);       // #FF8000
    private static readonly Color COLOR_DARK_BG = Color.FromArgb(230, 18, 18, 18); // #121212E6 (90% opacity)
    private static readonly Color COLOR_TEXT = Color.FromRgb(240, 240, 240);       // #F0F0F0
    private static readonly Color COLOR_MUTED = Color.FromRgb(136, 136, 136);      // #888888

    #endregion

    #region Sizing Constants

    private const double WIDGET_HEIGHT = 60;
    private const double WIDGET_WIDTH = 450;      // Fixed width for 3 columns
    private const double PADDING = 10;
    private const double BORDER_RADIUS = 6;
    private const double BORDER_THICKNESS = 2;
    private const double GLOW_BLUR_RADIUS = 8;

    #endregion

    #region Settings

    /// <summary>
    /// Whether to show turn names (true) or just turn numbers (false)
    /// </summary>
    public bool ShowTurnName { get; set; } = true;

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
        if (Config?.Settings != null && Config.Settings.TryGetValue("showTurnName", out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.True)
                ShowTurnName = true;
            else if (value is bool boolValue)
                ShowTurnName = boolValue;
        }
    }

    public void SaveSettings()
    {
        if (Config == null) return;
        Config.Settings["showTurnName"] = ShowTurnName;
        // Widget manager will handle actual save to file
    }

    private void InitializeWidget()
    {
        // Main canvas
        _mainCanvas = new Canvas
        {
            Width = WIDGET_WIDTH,
            Height = WIDGET_HEIGHT,
            Background = Brushes.Transparent
        };

        // Background border with rounded corners and glow
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

        // Content grid (3 equal columns)
        _contentGrid = new Grid
        {
            Width = WIDGET_WIDTH - (PADDING * 2),
            Height = WIDGET_HEIGHT - (PADDING * 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Define 3 equal columns + 2 separators
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) }); // Separator
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) }); // Separator
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // LAST TURN COLUMN
        _lastTurnPanel = CreateTurnPanel(out _lastTurnLabel, out _lastTurnNumber, out _lastTurnName, "LAST", COLOR_MUTED);
        Grid.SetColumn(_lastTurnPanel, 0);
        _contentGrid.Children.Add(_lastTurnPanel);

        // Separator 1
        var separator1 = new Border
        {
            Width = 1,
            Height = WIDGET_HEIGHT - (PADDING * 2),
            Background = new SolidColorBrush(COLOR_MUTED),
            Opacity = 0.3,
            Margin = new Thickness(8, 0, 8, 0)
        };
        Grid.SetColumn(separator1, 1);
        _contentGrid.Children.Add(separator1);

        // CURRENT TURN COLUMN
        _currentTurnPanel = CreateTurnPanel(out _currentTurnLabel, out _currentTurnNumber, out _currentTurnName, "CURRENT", COLOR_ORANGE);
        Grid.SetColumn(_currentTurnPanel, 2);
        _contentGrid.Children.Add(_currentTurnPanel);

        // Separator 2
        var separator2 = new Border
        {
            Width = 1,
            Height = WIDGET_HEIGHT - (PADDING * 2),
            Background = new SolidColorBrush(COLOR_MUTED),
            Opacity = 0.3,
            Margin = new Thickness(8, 0, 8, 0)
        };
        Grid.SetColumn(separator2, 3);
        _contentGrid.Children.Add(separator2);

        // NEXT TURN COLUMN
        _nextTurnPanel = CreateTurnPanel(out _nextTurnLabel, out _nextTurnNumber, out _nextTurnName, "NEXT", COLOR_TEAL);
        Grid.SetColumn(_nextTurnPanel, 4);
        _contentGrid.Children.Add(_nextTurnPanel);

        // Place content inside border
        _backgroundBorder.Child = _contentGrid;

        // Add border to canvas
        _mainCanvas.Children.Add(_backgroundBorder);

        // Set canvas as window content
        Content = _mainCanvas;
    }

    private StackPanel CreateTurnPanel(out TextBlock label, out TextBlock number, out TextBlock name, string labelText, Color accentColor)
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Label (LAST / CURRENT / NEXT)
        label = new TextBlock
        {
            Text = labelText,
            FontSize = 8,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(COLOR_MUTED),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };
        panel.Children.Add(label);

        // Turn number (T3)
        number = new TextBlock
        {
            Text = "—",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(accentColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        };
        panel.Children.Add(number);

        // Turn name (Parabolica)
        name = new TextBlock
        {
            Text = "",
            FontSize = 10,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(COLOR_TEXT),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 120,
            Visibility = ShowTurnName ? Visibility.Visible : Visibility.Collapsed
        };
        panel.Children.Add(name);

        return panel;
    }

    protected override void UpdateUI(TelemetryData data)
    {
        // Update turn name visibility if setting changed
        _lastTurnName.Visibility = ShowTurnName ? Visibility.Visible : Visibility.Collapsed;
        _currentTurnName.Visibility = ShowTurnName ? Visibility.Visible : Visibility.Collapsed;
        _nextTurnName.Visibility = ShowTurnName ? Visibility.Visible : Visibility.Collapsed;

        // Update last turn
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

        // Update current turn
        if (data.IsInTurn && data.TurnNumber > 0)
        {
            _currentTurnNumber.Text = $"T{data.TurnNumber}";
            _currentTurnName.Text = data.TurnName;
            
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
            _currentTurnNumber.Text = "—";
            _currentTurnName.Text = "";
            _backgroundBorder.BorderBrush = new SolidColorBrush(COLOR_TEAL);
            
            // Restore glow
            if (_backgroundBorder.Effect is DropShadowEffect glow)
            {
                glow.Color = COLOR_TEAL;
            }
        }

        // Update next turn
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
