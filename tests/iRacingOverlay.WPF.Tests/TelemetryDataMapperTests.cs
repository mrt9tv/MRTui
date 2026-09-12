using iRacingOverlay.Core.Models;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Tests;

/// <summary>
/// The mapper is the one place a TelemetryField name is tied to a TelemetryData
/// property. A field that maps to the wrong property renders a plausible number
/// with the wrong meaning, which no visual check catches.
/// </summary>
public class TelemetryDataMapperTests
{
    [Fact]
    public void NullData_YieldsNull()
    {
        Assert.Null(TelemetryDataMapper.GetValue(TelemetryField.Speed, null));
        Assert.Equal("--", TelemetryDataMapper.GetFormattedValue(TelemetryField.Speed, null));
    }

    [Theory]
    [InlineData(TelemetryField.RPM)]
    [InlineData(TelemetryField.Gear)]
    [InlineData(TelemetryField.Throttle)]
    [InlineData(TelemetryField.FuelLevel)]
    [InlineData(TelemetryField.LastLapTime)]
    [InlineData(TelemetryField.OptimalShiftRPM)]
    [InlineData(TelemetryField.DrsStatus)]
    [InlineData(TelemetryField.ErsBattery)]
    public void CommonFields_AreMapped(TelemetryField field)
    {
        var data = new TelemetryData();
        Assert.NotNull(TelemetryDataMapper.GetValue(field, data));
    }

    [Fact]
    public void ErsBattery_IsScaledToPercent()
    {
        var data = new TelemetryData { ErsBatteryPct = 0.42f };
        var value = Assert.IsType<float>(TelemetryDataMapper.GetValue(TelemetryField.ErsBattery, data));
        Assert.Equal(42f, value, precision: 3);
    }

    [Fact]
    public void ShiftFields_ReadTheServicePublishedValues()
    {
        var data = new TelemetryData { ShiftOptimalRPM = 8450f, ShiftRedlineRPM = 9000f };
        Assert.Equal(8450f, TelemetryDataMapper.GetValue(TelemetryField.OptimalShiftRPM, data));
        Assert.Equal(9000f, TelemetryDataMapper.GetValue(TelemetryField.Redline, data));
    }

    [Fact]
    public void EveryField_ResolvesWithoutThrowing()
    {
        // A new TelemetryField with no mapper entry would fall through to a
        // default arm; whatever that arm does, it must not throw on a blank frame.
        var data = new TelemetryData();
        foreach (var field in Enum.GetValues<TelemetryField>())
        {
            var ex = Record.Exception(() => TelemetryDataMapper.GetValue(field, data));
            Assert.True(ex == null, $"{field} threw {ex?.GetType().Name}: {ex?.Message}");
        }
    }
}
