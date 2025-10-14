using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Widgets.SpeedWidget;

/// <summary>
/// Simple speed display widget showing current speed
/// Built entirely in code to support custom base class
/// </summary>
public class SpeedWidget : WidgetBase
{
    private readonly TextBlock _speedText;
    private readonly TextBlock _unitText;
    private readonly Border _border;
    private bool _showKph = true;
    private bool _showMph = false;

    public override WidgetType WidgetType => WidgetType.Speed;

    public SpeedWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        // Load settings from config
        if (Config.Settings.TryGetValue("showKph", out var showKph))
        {
            _showKph = Convert.ToBoolean(showKph);
        }

        if (Config.Settings.TryGetValue("showMph", out var showMph))
        {
            _showMph = Convert.ToBoolean(showMph);
        }

        // Create UI elements in code
        Title = "Speed Widget";
        Width = 250;
        Height = 120;

        // Title text
        var titleText = new TextBlock
        {
            Text = "SPEED",
            Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        // Speed value text
        _speedText = new TextBlock
        {
            Text = "0",
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            FontSize = 48,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = new FontFamily("Consolas")
        };

        // Unit text
        _unitText = new TextBlock
        {
            Text = _showMph ? "mph" : "km/h",
            Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };

        // Stack panel to hold all elements
        var stackPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stackPanel.Children.Add(titleText);
        stackPanel.Children.Add(_speedText);
        stackPanel.Children.Add(_unitText);

        // Border with rounded corners
        _border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(221, 0, 0, 0)), // #DD000000
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            BorderThickness = new Thickness(2),
            Child = stackPanel
        };

        Content = _border;
    }

    protected override void UpdateUI(TelemetryData data)
    {
        // Update speed value
        if (_showMph)
        {
            _speedText.Text = $"{data.SpeedMph:F0}";
        }
        else
        {
            _speedText.Text = $"{data.SpeedKmh:F0}";
        }
    }

    protected override void OnConnectionStatusChanged(ConnectionStatus status)
    {
        // Change border color based on connection status
        switch (status)
        {
            case ConnectionStatus.Connected:
                _border.BorderBrush = Brushes.LimeGreen;
                break;
            case ConnectionStatus.Connecting:
            case ConnectionStatus.Reconnecting:
                _border.BorderBrush = Brushes.Yellow;
                break;
            case ConnectionStatus.Disconnected:
            case ConnectionStatus.Error:
                _border.BorderBrush = Brushes.Red;
                _speedText.Text = "---";
                break;
        }
    }
}
