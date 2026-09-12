using iRacingOverlay.Core.Models;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Widgets.MRTOneWidget;

namespace iRacingOverlay.WPF.Tests;

/// <summary>
/// The formatter is the last step before a value reaches the driver's eye, so
/// these pin the exact strings — a unit slip or a wrong label here goes
/// unnoticed until a race.
/// </summary>
public class MRTOneDataFormatterTests
{
    private static readonly TelemetryData Empty = new();

    private static string Fmt(TelemetryField field, object value, bool metric = true, TelemetryData? data = null) =>
        MRTOneDataFormatter.FormatValue(field, value, data ?? Empty, metric);

    // ── Units ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(50f, true, "180")]   // 50 m/s = 180 km/h
    [InlineData(50f, false, "111")]  // 111.8 mph, truncated
    [InlineData(0f, true, "0")]
    public void Speed_ConvertsFromMetresPerSecond(float mps, bool metric, string expected)
    {
        Assert.Equal(expected, Fmt(TelemetryField.Speed, mps, metric));
    }

    [Theory]
    [InlineData(40f, true, "40.00")]
    [InlineData(40f, false, "10.57")]  // US gallons
    public void FuelLevel_FollowsUnitSetting(float litres, bool metric, string expected)
    {
        Assert.Equal(expected, Fmt(TelemetryField.FuelLevel, litres, metric));
    }

    [Theory]
    [InlineData(true, "km/h")]
    [InlineData(false, "mph")]
    public void SpeedLabel_CarriesTheUnit(bool metric, string expected)
    {
        Assert.Equal(expected, MRTOneDataFormatter.GetLabel(TelemetryField.Speed, metric));
    }

    // ── Gear ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-1, "R")]
    [InlineData(0, "N")]
    [InlineData(3, "3")]
    public void Gear_UsesRAndN(int gear, string expected)
    {
        var data = new TelemetryData { Gear = gear };
        Assert.Equal(expected, Fmt(TelemetryField.Gear, gear, data: data));
    }

    // ── Lap times ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(92.345f, "1:32.345")]
    [InlineData(59.9f, "0:59.900")]
    [InlineData(0f, "--:--.---")]
    [InlineData(-1f, "--:--.---")]
    public void LapTime_IsMinutesSecondsThousandths(float seconds, string expected)
    {
        Assert.Equal(expected, MRTOneDataFormatter.FormatLapTime(seconds));
    }

    [Fact]
    public void LapTime_IsClampedBeforeFormatting()
    {
        // A garbage 10-hour value is clamped to the 600 s ceiling, not rendered.
        Assert.Equal("10:00.000", Fmt(TelemetryField.LastLapTime, 36000f));
    }

    // ── Percentages and counts ────────────────────────────────────────

    [Theory]
    [InlineData(0.756f, "75%")]
    [InlineData(1f, "100%")]
    public void Throttle_IsWholePercent(float value, string expected)
    {
        Assert.Equal(expected, Fmt(TelemetryField.Throttle, value));
    }

    [Fact]
    public void Incidents_CarryTheXSuffix()
    {
        Assert.Equal("7x", Fmt(TelemetryField.IncidentCount, 7));
    }

    [Fact]
    public void Position_HasThePPrefix()
    {
        Assert.Equal("P4", Fmt(TelemetryField.Position, 4));
    }

    // ── Shift geometry ────────────────────────────────────────────────

    [Theory]
    [InlineData(8500f, "8500")]
    [InlineData(0f, "-")]
    public void OptimalShiftRpm_IsWholeNumberOrDash(float rpm, string expected)
    {
        Assert.Equal(expected, Fmt(TelemetryField.OptimalShiftRPM, rpm));
    }

    // ── Hybrid / DRS ──────────────────────────────────────────────────
    // The 0/1/2 mapping is from the channel description and is marked
    // unverified in the formatter; this pins what the code does today.

    [Theory]
    [InlineData(0, "OFF")]
    [InlineData(1, "ARMED")]
    [InlineData(2, "OPEN")]
    [InlineData(5, "5")]
    public void DrsStatus_MapsKnownStates(int status, string expected)
    {
        Assert.Equal(expected, Fmt(TelemetryField.DrsStatus, status));
    }

    [Fact]
    public void ErsBattery_IsWholePercent()
    {
        Assert.Equal("64%", Fmt(TelemetryField.ErsBattery, 64.7f));
    }

    // ── Labels ────────────────────────────────────────────────────────

    [Fact]
    public void CustomLabel_OverridesDefault()
    {
        var custom = new Dictionary<string, string> { [TelemetryField.RPM.ToString()] = "REVS" };
        Assert.Equal("REVS", MRTOneDataFormatter.GetLabel(TelemetryField.RPM, true, custom));
        Assert.Equal("RPM", MRTOneDataFormatter.GetLabel(TelemetryField.RPM, true));
    }
}
