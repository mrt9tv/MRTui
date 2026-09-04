using System;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// One frame's worth of per-car (CarIdx[64]) telemetry arrays.
///
/// The telemetry service used to hand widgets either the SDK's own arrays or a
/// single set of pre-allocated scratch buffers. Both are reused every tick, and
/// widget rendering is dispatched asynchronously — so by the time a widget read
/// <c>CarIdxTrackSurface</c>, the telemetry thread had already overwritten it,
/// sometimes mid-loop. That produced intermittently wrong radar states and cars
/// flickering in and out of the relative table.
///
/// <see cref="CarIdxFramePool"/> hands out one of these per tick from a small
/// rotating pool, so a frame in flight is never the frame being written.
/// </summary>
public sealed class CarIdxFrame
{
    public const int MaxCars = 64;

    public readonly float[] LapDistPct = new float[MaxCars];
    public readonly bool[] OnPitRoad = new bool[MaxCars];
    public readonly int[] TrackSurface = new int[MaxCars];
    public readonly int[] Class = new int[MaxCars];
    public readonly int[] Lap = new int[MaxCars];
    public readonly int[] Position = new int[MaxCars];
    public readonly int[] ClassPosition = new int[MaxCars];
    public readonly int[] Gear = new int[MaxCars];
    public readonly float[] RPM = new float[MaxCars];
    public readonly float[] EstTime = new float[MaxCars];
    public readonly float[] F2Time = new float[MaxCars];
    public readonly float[] LastLapTime = new float[MaxCars];
    public readonly float[] BestLapTime = new float[MaxCars];
    public readonly int[] SessionFlags = new int[MaxCars];
    public readonly int[] BestLapNum = new int[MaxCars];
    public readonly int[] LapCompleted = new int[MaxCars];
    public readonly int[] FastRepairsUsed = new int[MaxCars];
    public readonly int[] P2PCount = new int[MaxCars];
    public readonly bool[] P2PStatus = new bool[MaxCars];
    public readonly int[] PaceFlags = new int[MaxCars];
    public readonly int[] PaceLine = new int[MaxCars];
    public readonly int[] PaceRow = new int[MaxCars];
    public readonly int[] QualTireCompound = new int[MaxCars];
    public readonly bool[] QualTireCompoundLocked = new bool[MaxCars];
    public readonly float[] Steer = new float[MaxCars];
    public readonly int[] TireCompound = new int[MaxCars];
    public readonly int[] TrackSurfaceMaterial = new int[MaxCars];
    public readonly bool[] RecentIncident = new bool[MaxCars];
    public readonly int[] RecentIncidentDelta = new int[MaxCars];

    /// <summary>Copy a source array into a destination buffer, zeroing any unfilled tail.</summary>
    public static void Copy<T>(T[]? source, T[] destination) where T : struct
    {
        if (source == null)
        {
            Array.Clear(destination);
            return;
        }

        int len = Math.Min(source.Length, destination.Length);
        Array.Copy(source, destination, len);
        if (len < destination.Length)
            Array.Clear(destination, len, destination.Length - len);
    }

    /// <summary>Copy an enum array into an int buffer, zeroing any unfilled tail.</summary>
    public static void CopyEnum<TEnum>(TEnum[]? source, int[] destination) where TEnum : struct, Enum
    {
        if (source == null)
        {
            Array.Clear(destination);
            return;
        }

        int len = Math.Min(source.Length, destination.Length);
        for (int i = 0; i < len; i++)
            destination[i] = Convert.ToInt32(source[i]);
        for (int i = len; i < destination.Length; i++)
            destination[i] = 0;
    }
}

/// <summary>
/// Small rotating pool of <see cref="CarIdxFrame"/> instances.
///
/// Depth 4 is ample: widgets coalesce to at most one pending render each, so a
/// frame is consumed within a tick or two of being published. Rotating rather
/// than allocating keeps the 60 Hz path allocation-free.
/// </summary>
public sealed class CarIdxFramePool
{
    private const int Depth = 4;

    private readonly CarIdxFrame[] _frames = new CarIdxFrame[Depth];
    private int _next;

    public CarIdxFramePool()
    {
        for (int i = 0; i < Depth; i++)
            _frames[i] = new CarIdxFrame();
    }

    /// <summary>Take the next frame in the rotation. Called once per telemetry tick.</summary>
    public CarIdxFrame Next()
    {
        var frame = _frames[_next];
        _next = (_next + 1) % Depth;
        return frame;
    }
}
