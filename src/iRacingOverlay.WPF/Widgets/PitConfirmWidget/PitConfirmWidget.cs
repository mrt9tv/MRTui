using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Core;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF.Widgets.PitConfirmWidget;

/// <summary>
/// Pit service confirmation.
///
/// Arriving in the box with the wrong service armed is the highest-consequence
/// mistake this overlay can prevent: no fuel selected on a stop you needed fuel
/// for, or four tires when you only wanted two, decides races. iRacing publishes
/// exactly what is armed (<c>dpFuelFill</c>, <c>dpFuelAddKg</c>, <c>dpTireChange</c>,
/// <c>dpFastRepair</c>) and whether the pit lane is even open — none of which the
/// app read before.
///
/// The panel stays hidden until you are approaching or on pit road, then shows the
/// armed service and turns amber when something looks wrong. The fuel check closes a
/// loop the app already had half of: FuelCalculatorService knows how much you need.
/// </summary>
public partial class PitConfirmWidget : WidgetBase
{
    #region Constants

    private const double WIDGET_WIDTH = 210;
    private const double PADDING = 8;
    private const double ROW_HEIGHT = 20;
    private const double HEADER_HEIGHT = 18;
    private const double BORDER_RADIUS = 5;

    /// <summary>Litres of disagreement with the fuel plan before we warn.</summary>
    private const float FUEL_MISMATCH_TOLERANCE_L = 2.0f;

    /// <summary>iRacing TrackSurface values that mean "in the pit lane".</summary>
    private const int SURFACE_IN_PIT_STALL = 1;
    private const int SURFACE_APPROACHING_PITS = 2;

    /// <summary>1 kg of iRacing fuel is this many litres (methanol/gasoline approximation).</summary>
    private const float KG_TO_LITRES = 1.0f / 0.75f;

    #endregion

    #region Colors

    private static readonly Color COLOR_BG = Color.FromArgb(228, 18, 18, 18);
    private static readonly Color COLOR_TEAL = Color.FromRgb(0, 240, 240);
    private static readonly Color COLOR_TEXT = Color.FromRgb(210, 210, 210);
    private static readonly Color COLOR_MUTED = Color.FromRgb(120, 120, 120);
    private static readonly Color COLOR_OK = Color.FromRgb(80, 210, 130);
    private static readonly Color COLOR_WARN = Color.FromRgb(255, 176, 0);
    private static readonly Color COLOR_ALARM = Color.FromRgb(255, 70, 60);

    #endregion

    #region State

    private Canvas _canvas = null!;
    private Border _background = null!;
    private TextBlock _title = null!;
    private TextBlock _fuelRow = null!;
    private TextBlock _tyreRow = null!;
    private TextBlock _extraRow = null!;
    private TextBlock _warningRow = null!;

    private int _blinkFrame;

    #endregion

    /// <summary>Also show the panel in the garage so service can be checked before going out.</summary>
    public bool ShowInGarage { get; set; }

    public override WidgetType WidgetType => WidgetType.PitConfirm;

    /// <summary>Slow-changing panel — 6 Hz is plenty.</summary>
    protected override int UpdateIntervalTicks => 10;

    public PitConfirmWidget(ITelemetryService telemetryService, WidgetConfig? config = null)
        : base(telemetryService, config)
    {
        BuildVisuals();
        LoadSettings();
    }

    private void BuildVisuals()
    {
        Width = WIDGET_WIDTH;
        Height = PADDING * 2 + HEADER_HEIGHT + ROW_HEIGHT * 4;

        _canvas = new Canvas { Background = Brushes.Transparent };

        _background = new Border
        {
            Width = WIDGET_WIDTH,
            Height = Height,
            CornerRadius = new CornerRadius(BORDER_RADIUS),
            Background = BrushCache.Get(COLOR_BG.A, COLOR_BG.R, COLOR_BG.G, COLOR_BG.B),
            BorderBrush = BrushCache.Get(COLOR_TEAL),
            BorderThickness = new Thickness(1),
        };
        _canvas.Children.Add(_background);

        double y = PADDING;

        _title = AddText("PIT SERVICE", PADDING, y, 10, COLOR_TEAL, FontWeights.Bold);
        y += HEADER_HEIGHT;

        _fuelRow = AddText("", PADDING, y, 12, COLOR_TEXT, FontWeights.SemiBold);
        y += ROW_HEIGHT;

        _tyreRow = AddText("", PADDING, y, 12, COLOR_TEXT, FontWeights.SemiBold);
        y += ROW_HEIGHT;

        _extraRow = AddText("", PADDING, y, 11, COLOR_MUTED, FontWeights.Normal);
        y += ROW_HEIGHT;

        _warningRow = AddText("", PADDING, y, 11, COLOR_WARN, FontWeights.Bold);

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

    protected override void UpdateUI(TelemetryData data)
    {
        _blinkFrame++;

        bool inPitContext = data.OnPitRoad
                            || data.PlayerCarInPitStall
                            || data.PlayerTrackSurface == SURFACE_IN_PIT_STALL
                            || data.PlayerTrackSurface == SURFACE_APPROACHING_PITS
                            || (ShowInGarage && data.IsInGarage);

        // Only occupy the screen when it is relevant.
        UiUpdate.SetVisibility(_canvas, inPitContext ? Visibility.Visible : Visibility.Collapsed);
        if (!inPitContext) return;

        RenderFuel(data);
        RenderTyres(data);
        RenderExtras(data);
        RenderWarnings(data);
    }

    private void RenderFuel(TelemetryData data)
    {
        if (!data.PitSvFuelArmed)
        {
            // On cars with an auto-fill crew, "not armed" is the normal state and
            // the tank still gets filled. Say so rather than muting it.
            if (data.PitSvFuelAutoFillActive)
            {
                UiUpdate.SetText(_fuelRow, "FUEL    auto-fill");
                UiUpdate.SetForeground(_fuelRow, BrushCache.Get(COLOR_OK));
                return;
            }

            UiUpdate.SetText(_fuelRow, "FUEL    not armed");
            UiUpdate.SetForeground(_fuelRow, BrushCache.Get(COLOR_MUTED));
            return;
        }

        float litres = data.PitSvFuelAddKg * KG_TO_LITRES;
        UiUpdate.SetText(_fuelRow, $"FUEL    +{litres:F1} L");
        UiUpdate.SetForeground(_fuelRow, BrushCache.Get(COLOR_OK));
    }

    private void RenderTyres(TelemetryData data)
    {
        if (data.PitSvTiresArmed)
        {
            UiUpdate.SetText(_tyreRow, "TYRES   4 corners");
            UiUpdate.SetForeground(_tyreRow, BrushCache.Get(COLOR_OK));
        }
        else
        {
            UiUpdate.SetText(_tyreRow, "TYRES   none");
            UiUpdate.SetForeground(_tyreRow, BrushCache.Get(COLOR_MUTED));
        }
    }

    private void RenderExtras(TelemetryData data)
    {
        string extras = "";

        if (data.PitSvFastRepairArmed)
            extras = $"Fast repair armed ({data.FastRepairAvailable} left)";
        else if (data.PitRepairLeft > 0.1f)
            extras = $"Repairs {data.PitRepairLeft:F0}s";
        else if (data.PitSvChargeAddKWh > 0.01f)
            extras = $"Charge +{data.PitSvChargeAddKWh:F1} kWh";
        else if (data.PitSvQTape > 0.5f)
            extras = $"Grille tape {data.PitSvQTape:F0}";
        else if (Math.Abs(data.PitSvWeightJackerLeft) > 0.01f || Math.Abs(data.PitSvWeightJackerRight) > 0.01f)
            extras = $"Jacker L {data.PitSvWeightJackerLeft:+0.0;-0.0}  R {data.PitSvWeightJackerRight:+0.0;-0.0}";
        // 255 is iRacing's "unlimited" sentinel, per the channel description.
        else if (data.TireSetsAvailable > 0 && data.TireSetsAvailable != 255)
            extras = $"{data.TireSetsAvailable} tyre sets left";

        UiUpdate.SetText(_extraRow, extras);
    }

    private void RenderWarnings(TelemetryData data)
    {
        var (message, color, blink) = EvaluateWarning(data);

        // Blink the alarming ones so they cannot be missed at a glance.
        bool blinkOff = blink && (_blinkFrame / 3) % 2 == 1;
        UiUpdate.SetText(_warningRow, blinkOff ? "" : message);
        UiUpdate.SetForeground(_warningRow, BrushCache.Get(color));
    }

    /// <summary>
    /// Decide what, if anything, is worth warning about. Kept separate from rendering
    /// so the precedence is readable and testable.
    /// </summary>
    internal static (string Message, Color Color, bool Blink) EvaluateWarning(TelemetryData data)
    {
        if (!data.PitsOpen)
            return ("PITS CLOSED", COLOR_ALARM, true);

        // Auto-fill counts as fuel being handled; without this the "nothing armed"
        // and "need fuel" alarms fired on every stop in cars whose crew fills
        // automatically, which is exactly the kind of false alarm that gets a
        // warning widget switched off.
        bool fuelHandled = data.PitSvFuelArmed || data.PitSvFuelAutoFillActive;

        bool nothingArmed = !fuelHandled && !data.PitSvTiresArmed && !data.PitSvFastRepairArmed;
        if (nothingArmed)
            return ("NOTHING ARMED", COLOR_ALARM, true);

        // Cross-check the armed fuel against what the fuel calculator says is needed.
        // The app already computes this; it just never compared the two.
        float needed = data.FuelNeededToFinishL;
        if (needed > 0 && data.PitSvFuelArmed)
        {
            float armed = data.PitSvFuelAddKg * KG_TO_LITRES;
            float shortfall = needed - armed;
            if (shortfall > FUEL_MISMATCH_TOLERANCE_L)
                return ($"SHORT {shortfall:F1} L", COLOR_ALARM, true);
        }
        else if (needed > FUEL_MISMATCH_TOLERANCE_L && !fuelHandled)
        {
            return ($"NEED {needed:F1} L", COLOR_ALARM, true);
        }

        return ("", COLOR_WARN, false);
    }

    protected override void OnBackgroundOpacityChanged(double opacity)
    {
        if (_background == null) return;
        byte alpha = (byte)(255 * Math.Clamp(opacity, 0.0, 1.0));
        _background.Background = BrushCache.Get(alpha, COLOR_BG.R, COLOR_BG.G, COLOR_BG.B);
    }

    #region Settings persistence

    protected override void SaveWidgetSettings()
    {
        Config.Settings["ShowInGarage"] = ShowInGarage;
    }

    private void LoadSettings()
    {
        if (Config.Settings.TryGetValue("ShowInGarage", out var raw)
            && bool.TryParse(raw?.ToString(), out var v))
        {
            ShowInGarage = v;
        }
    }

    /// <summary>Persist this widget's settings into the layout.</summary>
    public void SaveSettings() => SaveWidgetSettings();

    #endregion
}
