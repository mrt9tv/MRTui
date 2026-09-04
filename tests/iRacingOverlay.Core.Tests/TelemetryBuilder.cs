using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Tests;

/// <summary>
/// Builds synthetic <see cref="TelemetryData"/> frames so the calculators can be
/// exercised without iRacing running. Core has no WPF dependency, which is what
/// makes this possible at all.
/// </summary>
public sealed class TelemetryBuilder
{
    public const int MaxCars = 64;

    private const int SurfaceNotInWorld = -1;
    private const int SurfaceOnTrack = 3;
    private const int SurfaceInPitStall = 1;

    private readonly TelemetryData _data;

    public TelemetryBuilder(int playerIdx = 0, float trackLength = 5.0f)
    {
        _data = new TelemetryData
        {
            PlayerCarIdx = playerIdx,
            TrackLength = trackLength,
            SessionType = "Race",
            SessionState = 4, // Racing
            CarIdxLapDistPct = new float[MaxCars],
            CarIdxOnPitRoad = new bool[MaxCars],
            CarIdxTrackSurface = new int[MaxCars],
            CarIdxClass = new int[MaxCars],
            CarIdxLap = new int[MaxCars],
            CarIdxPosition = new int[MaxCars],
            CarIdxClassPosition = new int[MaxCars],
            CarIdxGear = new int[MaxCars],
            CarIdxRPM = new float[MaxCars],
            CarIdxEstTime = new float[MaxCars],
            CarIdxF2Time = new float[MaxCars],
            CarIdxLastLapTime = new float[MaxCars],
            CarIdxBestLapTime = new float[MaxCars],
            CarIdxSessionFlags = new int[MaxCars],
            CarIdxBestLapNum = new int[MaxCars],
            CarIdxLapCompleted = new int[MaxCars],
            CarIdxRecentIncident = new bool[MaxCars],
            CarIdxRecentIncidentDelta = new int[MaxCars],
            CarIdxToDriverName = new Dictionary<int, string>(),
            CarIdxToCarNumber = new Dictionary<int, string>(),
        };

        // Everything starts as "not in the world" — tests opt cars in explicitly.
        for (int i = 0; i < MaxCars; i++)
            _data.CarIdxTrackSurface![i] = SurfaceNotInWorld;
    }

    /// <summary>Place a car on track at a given lap fraction.</summary>
    public TelemetryBuilder WithCar(
        int carIdx,
        float lapDistPct,
        int position = 0,
        int lap = 1,
        string driverName = "",
        int carClass = 0,
        bool onPitRoad = false,
        float lastLapTime = 90f)
    {
        _data.CarIdxLapDistPct![carIdx] = lapDistPct;
        _data.CarIdxTrackSurface![carIdx] = onPitRoad ? SurfaceInPitStall : SurfaceOnTrack;
        _data.CarIdxOnPitRoad![carIdx] = onPitRoad;
        _data.CarIdxPosition![carIdx] = position == 0 ? carIdx + 1 : position;
        _data.CarIdxClassPosition![carIdx] = _data.CarIdxPosition[carIdx];
        _data.CarIdxLap![carIdx] = lap;
        _data.CarIdxClass![carIdx] = carClass;
        _data.CarIdxLastLapTime![carIdx] = lastLapTime;
        _data.CarIdxBestLapTime![carIdx] = lastLapTime;
        _data.CarIdxToDriverName![carIdx] = string.IsNullOrEmpty(driverName) ? $"Driver {carIdx}" : driverName;
        _data.CarIdxToCarNumber![carIdx] = carIdx.ToString();

        if (carIdx == _data.PlayerCarIdx)
        {
            _data.LapDistPct = lapDistPct;
            _data.Lap = lap;
        }

        return this;
    }

    /// <summary>Fill a grid of <paramref name="count"/> cars evenly spaced around the lap.</summary>
    public TelemetryBuilder WithEvenlySpacedField(int count)
    {
        for (int i = 0; i < count; i++)
            WithCar(i, i / (float)count, position: i + 1);
        return this;
    }

    public TelemetryBuilder WithSessionType(string sessionType)
    {
        _data.SessionType = sessionType;
        return this;
    }

    public TelemetryBuilder WithSessionState(int state)
    {
        _data.SessionState = state;
        return this;
    }

    public TelemetryBuilder With(Action<TelemetryData> mutate)
    {
        mutate(_data);
        return this;
    }

    public TelemetryData Build() => _data;
}
