namespace iRacingOverlay.Core.Models;

/// <summary>
/// One row in the full-field standings table.
/// Sorted by overall race position (or best-lap in practice/qualifying).
/// Designed for maximum configurability — each property maps to a toggleable column.
/// </summary>
public class StandingsEntry
{
    // ── Identity ─────────────────────────────────────────────────────
    public int CarIdx { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string CarNumber { get; set; } = string.Empty;
    public int CarClassId { get; set; }
    public string CarModel { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;

    // ── Position ─────────────────────────────────────────────────────
    public int OverallPosition { get; set; }
    public int ClassPosition { get; set; }

    /// <summary>Position change since session start (positive = gained).</summary>
    public int PositionChange { get; set; }

    // ── Timing ───────────────────────────────────────────────────────
    /// <summary>Gap to leader in seconds (0 for leader).</summary>
    public float GapToLeader { get; set; }

    /// <summary>Interval to car directly ahead in seconds (0 for P1).</summary>
    public float Interval { get; set; }

    public float LastLapTime { get; set; }
    public float BestLapTime { get; set; }
    public int CurrentLap { get; set; }

    /// <summary>Fraction of current lap completed (0–1); used for smooth gap interpolation.</summary>
    public float LapDistPct { get; set; }

    // ── Status ───────────────────────────────────────────────────────
    public bool IsOnPitRoad { get; set; }
    public bool IsPlayer { get; set; }
    public bool IsConnected { get; set; } = true;
    public int PitStopCount { get; set; }

    // ── Driver info ──────────────────────────────────────────────────
    public int IRating { get; set; }
    public string LicenseClass { get; set; } = string.Empty;

    /// <summary>Laps behind leader (negative = lapped).</summary>
    public int LapDelta { get; set; }
}
