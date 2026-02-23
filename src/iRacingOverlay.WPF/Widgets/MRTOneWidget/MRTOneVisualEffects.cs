using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Telemetry;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF.Widgets.MRTOneWidget;

/// <summary>
/// Manages visual effects for MRT One widget (gradient, glow, RPM bead animation)
/// Encapsulates WPF-specific visual enhancement logic
/// Extracted from MRTOneWidget for better separation of concerns
/// </summary>
public class MRTOneVisualEffects
{
    private readonly Grid _mainGrid;
    private readonly Ellipse _gaugeCircle;
    private readonly TextBlock _centerValueText;

    // RPM indicator bead elements
    private Ellipse? _rpmIndicatorBead;
    private DispatcherTimer? _rpmBeadAnimationTimer;
    private TelemetryData? _lastTelemetryData;

    private Color _secondaryColor;

    public MRTOneVisualEffects(Grid mainGrid, Ellipse gaugeCircle, TextBlock centerValueText, Color secondaryColor)
    {
        _mainGrid = mainGrid ?? throw new ArgumentNullException(nameof(mainGrid));
        _gaugeCircle = gaugeCircle ?? throw new ArgumentNullException(nameof(gaugeCircle));
        _centerValueText = centerValueText ?? throw new ArgumentNullException(nameof(centerValueText));
        _secondaryColor = secondaryColor;
    }

    #region Gradient Background

    /// <summary>
    /// Apply radial gradient to gauge circle for depth effect
    /// Lighter center (30,30,30) → darker edge (10,10,10)
    /// </summary>
    public void ApplyGradientBackground(double opacity)
    {
        var gradient = new RadialGradientBrush();

        // Center: lighter gray
        gradient.GradientStops.Add(new GradientStop(
            Color.FromArgb((byte)(255 * opacity), 30, 30, 30),
            0.0));

        // Edge: darker gray
        gradient.GradientStops.Add(new GradientStop(
            Color.FromArgb((byte)(255 * opacity), 10, 10, 10),
            1.0));

        _gaugeCircle.Fill = gradient;
    }

    /// <summary>
    /// Remove gradient and restore solid fill
    /// </summary>
    public void RemoveGradientBackground(double opacity)
    {
        _gaugeCircle.Fill = new SolidColorBrush(Color.FromArgb(
            (byte)(255 * opacity), 20, 20, 20));
    }

    #endregion

    #region Glow Effects

    /// <summary>
    /// Apply drop shadow glow to center text and gauge circle
    /// </summary>
    public void ApplyGlowEffects(Color primaryColor)
    {
        // Glow on center value (gear)
        _centerValueText.Effect = new DropShadowEffect
        {
            Color = primaryColor,
            BlurRadius = 15,  // GLOW_BLUR_RADIUS_CENTER
            ShadowDepth = 0,
            Opacity = 0.8  // GLOW_OPACITY_CENTER
        };

        // Glow on gauge circle border
        _gaugeCircle.Effect = new DropShadowEffect
        {
            Color = primaryColor,
            BlurRadius = 10,  // GLOW_BLUR_RADIUS_GAUGE
            ShadowDepth = 0,
            Opacity = 0.6  // GLOW_OPACITY_GAUGE
        };
    }

    /// <summary>
    /// Remove glow effects
    /// </summary>
    public void RemoveGlowEffects()
    {
        _centerValueText.Effect = null;
        _gaugeCircle.Effect = null;
    }

    #endregion

    #region RPM Indicator Bead Animation

    /// <summary>
    /// Create and start RPM indicator bead animation
    /// Bead travels on gauge circle from bottom (0% RPM) to top (100% RPM)
    /// </summary>
    public void CreateRPMBead()
    {
        RemoveRPMBead(); // Clean up existing

        // Create bead
        _rpmIndicatorBead = new Ellipse
        {
            Width = 12,  // RPM_BEAD_SIZE
            Height = 12,  // RPM_BEAD_SIZE
            Fill = new SolidColorBrush(_secondaryColor),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new TranslateTransform(),
            Visibility = Visibility.Collapsed  // Start hidden
        };

        // Add to grid (above gauge circle but below text)
        _mainGrid.Children.Insert(1, _rpmIndicatorBead);

        // Start animation timer (~30 FPS)
        _rpmBeadAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)  // RPM_ANIMATION_INTERVAL_MS
        };
        _rpmBeadAnimationTimer.Tick += UpdateRPMBead;
        _rpmBeadAnimationTimer.Start();

        // Immediate update to avoid flash
        UpdateRPMBead(null, EventArgs.Empty);
    }

    /// <summary>
    /// Remove RPM bead and stop animation
    /// </summary>
    public void RemoveRPMBead()
    {
        if (_rpmIndicatorBead != null)
        {
            _mainGrid.Children.Remove(_rpmIndicatorBead);
            _rpmIndicatorBead = null;
        }

        if (_rpmBeadAnimationTimer != null)
        {
            _rpmBeadAnimationTimer.Stop();
            _rpmBeadAnimationTimer.Tick -= UpdateRPMBead;
            _rpmBeadAnimationTimer = null;
        }
    }

    /// <summary>
    /// Update bead position based on current RPM
    /// Bead travels from bottom (90°) to top (270°) = right half of circle
    /// </summary>
    private void UpdateRPMBead(object? sender, EventArgs e)
    {
        if (_rpmIndicatorBead == null || _lastTelemetryData == null)
            return;

        var rpm = _lastTelemetryData.RPM;
        var zone = ShiftPointCalculator.GetRPMZone(
            rpm,
            _lastTelemetryData.Gear,
            _lastTelemetryData.PlayerCarSLFirstRPM,
            _lastTelemetryData.PlayerCarSLShiftRPM,
            _lastTelemetryData.PlayerCarSLLastRPM,
            _lastTelemetryData.PlayerCarSLBlinkRPM);

        // Get redline (prefer telemetry, fallback to estimate)
        double redline = _lastTelemetryData.EngineRedlineRPM > 0
            ? _lastTelemetryData.EngineRedlineRPM
            : ShiftPointCalculator.GetEstimatedRedline();

        if (redline < 3000) redline = 8000; // Safety fallback

        // Hide if no valid RPM
        if (rpm <= 0 || redline <= 0)
        {
            _rpmIndicatorBead.Visibility = Visibility.Collapsed;
            return;
        }

        // Map RPM to angle (90° to 270° = right half of circle)
        double percentage = Math.Min(rpm / redline, 1.0);
        double angle = 90 + (percentage * 180);
        double angleRad = angle * Math.PI / 180;

        // Calculate bead position on circle
        double centerX = _gaugeCircle.ActualWidth / 2;
        double centerY = _gaugeCircle.ActualHeight / 2;
        double gaugeRadius = Math.Min(_gaugeCircle.ActualWidth, _gaugeCircle.ActualHeight) / 2;
        double adjustedRadius = gaugeRadius - (_gaugeCircle.StrokeThickness / 2);

        double beadX = adjustedRadius * Math.Cos(angleRad);
        double beadY = adjustedRadius * Math.Sin(angleRad);

        // Smooth horizontal centering near top (prevents flicker at redline)
        const double RPM_CENTER_ANGLE_THRESHOLD = 260;
        const double RPM_CENTER_SMOOTHING = 10.0;

        if (angle >= RPM_CENTER_ANGLE_THRESHOLD)
        {
            double centeringFactor = Math.Min(1.0,
                (angle - RPM_CENTER_ANGLE_THRESHOLD) / RPM_CENTER_SMOOTHING);
            beadX = beadX * (1.0 - centeringFactor);
        }

        // Round for crisp rendering
        beadX = Math.Round(beadX);
        beadY = Math.Round(beadY);

        // Apply transform
        if (_rpmIndicatorBead.RenderTransform is TranslateTransform transform)
        {
            transform.X = beadX;
            transform.Y = beadY;
        }

        // Show bead after positioning
        _rpmIndicatorBead.Visibility = Visibility.Visible;

        // Color based on RPM zone
        Color beadColor = zone switch
        {
            ShiftPointCalculator.RPMZone.Danger => Colors.Red,
            ShiftPointCalculator.RPMZone.Optimal => _secondaryColor, // Orange
            ShiftPointCalculator.RPMZone.Warning => Colors.Yellow,
            _ => Color.FromRgb(0, 128, 128) // Teal for safe zone
        };
        _rpmIndicatorBead.Fill = BrushCache.Get(beadColor);
    }

    /// <summary>
    /// Update telemetry data for RPM bead animation
    /// Must be called from widget's UpdateUI method
    /// </summary>
    public void UpdateTelemetryData(TelemetryData data)
    {
        _lastTelemetryData = data;
    }

    /// <summary>
    /// Update secondary color when theme changes
    /// </summary>
    public void UpdateSecondaryColor(Color color)
    {
        _secondaryColor = color;
        if (_rpmIndicatorBead != null)
        {
            _rpmIndicatorBead.Fill = BrushCache.Get(color);
        }
    }

    #endregion
}
