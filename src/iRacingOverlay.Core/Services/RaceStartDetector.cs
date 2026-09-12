using System;
using iRacingOverlay.Core.Models;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Times the driver's reaction to the start lights, and names the launch
/// technique.
///
/// Standing starts: iRacing raises StartReady, then StartSet, then StartGo in
/// SessionFlags. The rising edge of StartGo is lights-out. The car's state on
/// that frame — gear, clutch pedal, throttle — is the baseline everything is
/// measured against, which is what makes a driver who holds 6000 rpm with the
/// clutch down measurable at all: the reaction is the clutch coming up, not the
/// throttle that was already there.
///
/// Rolling starts have no lights. The green flag (or the session entering the
/// racing state, whichever lands first) is the mark, and the reaction is a
/// throttle increase over what the driver was holding at pace speed.
///
/// A jump start — the car moving while the lights are still up — ends the
/// measurement immediately; there is no reaction time to report after one.
///
/// All timing is in SessionTime, the sim's own clock, so the overlay's delivery
/// latency does not enter into it. Frames arrive at 60 Hz, so every number here
/// carries ~16 ms of uncertainty on each end.
/// </summary>
public sealed class RaceStartDetector
{
    // SessionFlags bits for the standing-start light sequence, plus green.
    private const uint FlagGreen = 0x00000004;
    private const uint FlagStartReady = 0x20000000;
    private const uint FlagStartSet = 0x40000000;
    private const uint FlagStartGo = 0x80000000;
    private const uint FlagStartAny = FlagStartReady | FlagStartSet | FlagStartGo;

    private const int SessionStateParadeLaps = 3;
    private const int SessionStateRacing = 4;

    /// <summary>Throttle must rise this far above the lights-out baseline to count.</summary>
    private const float ThrottleRise = 0.10f;

    /// <summary>Clutch pedal must come up this far (toward engaged) to count as a release.</summary>
    private const float ClutchRise = 0.15f;

    /// <summary>Below this the clutch pedal counts as held down at lights-out.</summary>
    private const float ClutchHeldBelow = 0.5f;

    /// <summary>Speed gain that counts as the car moving — a jump before the lights, a launch after.</summary>
    private const float MoveSpeed = 0.5f;

    /// <summary>Give up on a start that produces no input — a stall, or the driver is away.</summary>
    private const double TimeoutSeconds = 5.0;

    private enum Phase { Idle, ArmedStanding, ArmedRolling, Timing, Done }

    private Phase _phase = Phase.Idle;
    private int _sessionNum = -1;
    private uint _prevFlags;
    private int _prevState;

    // Baseline at lights-out
    private double _goTime;
    private float _baseThrottle, _baseClutch, _baseSpeed;
    private int _baseGear;
    private LaunchTechnique _technique;

    // Timing progress
    private float _reaction;
    private LaunchInput _firstInput;

    private float _best;

    /// <summary>The most recent completed start, or null before the first.</summary>
    public RaceStartResult? Result { get; private set; }

    /// <summary>True only on the tick <see cref="Result"/> was produced. Consumers announce on this.</summary>
    public bool JustMeasured { get; private set; }

    /// <summary>Clear everything, including the best. Call on disconnect.</summary>
    public void Reset()
    {
        _phase = Phase.Idle;
        _sessionNum = -1;
        _prevFlags = 0;
        _prevState = 0;
        _best = 0f;
        Result = null;
        JustMeasured = false;
    }

    public void Update(TelemetryData data)
    {
        JustMeasured = false;

        // A new session is a new start. The best carries across, since a
        // driver's reaction is theirs rather than the session's.
        if (data.SessionNum != _sessionNum)
        {
            _sessionNum = data.SessionNum;
            _phase = Phase.Idle;
            _prevFlags = 0;
            _prevState = 0;
        }

        uint flags = data.SessionFlags;
        uint raised = flags & ~_prevFlags;
        int state = data.SessionState;

        switch (_phase)
        {
            case Phase.Idle:
                // Lights coming up is a standing start. Parade laps with no
                // lights is a rolling one.
                if ((flags & (FlagStartReady | FlagStartSet)) != 0)
                    _phase = Phase.ArmedStanding;
                else if (state == SessionStateParadeLaps)
                    _phase = Phase.ArmedRolling;
                break;

            case Phase.ArmedStanding:
                if ((raised & FlagStartGo) != 0)
                {
                    Snapshot(data, rolling: false);
                }
                else if ((flags & (FlagStartReady | FlagStartSet)) != 0
                         && data.Speed > MoveSpeed && data.IsOnTrack)
                {
                    Finish(data, jumpStart: true);
                }
                else if ((flags & FlagStartAny) == 0 && state != SessionStateParadeLaps)
                {
                    // Lights went away without a go — the start was aborted.
                    _phase = Phase.Idle;
                }
                break;

            case Phase.ArmedRolling:
                if ((flags & (FlagStartReady | FlagStartSet)) != 0)
                {
                    // It has lights after all.
                    _phase = Phase.ArmedStanding;
                }
                else if ((raised & FlagGreen) != 0
                         || (state == SessionStateRacing && _prevState == SessionStateParadeLaps))
                {
                    Snapshot(data, rolling: true);
                }
                break;

            case Phase.Timing:
                Time(data);
                break;

            case Phase.Done:
                // Re-arm for a restart: the session drops back out of racing.
                if (state != SessionStateRacing && _prevState == SessionStateRacing)
                    _phase = Phase.Idle;
                break;
        }

        _prevFlags = flags;
        _prevState = state;
    }

    private void Snapshot(TelemetryData data, bool rolling)
    {
        _goTime = data.SessionTime;
        _baseThrottle = data.ThrottleRaw;
        _baseClutch = data.ClutchRaw;
        _baseSpeed = data.Speed;
        _baseGear = data.Gear;
        _reaction = 0f;
        _firstInput = LaunchInput.None;

        _technique = rolling ? LaunchTechnique.Rolling
            : _baseGear <= 0 ? LaunchTechnique.NeutralToGear
            : _baseClutch < ClutchHeldBelow ? LaunchTechnique.Clutch
            : LaunchTechnique.ThrottleOnly;

        _phase = Phase.Timing;
    }

    private void Time(TelemetryData data)
    {
        double elapsed = data.SessionTime - _goTime;

        if (_firstInput == LaunchInput.None)
        {
            // First input wins. Order matters only for a frame where two land
            // together; the gear change is the most deliberate, so it is checked first.
            if (_baseGear <= 0 && data.Gear > 0)
                _firstInput = LaunchInput.Gear;
            else if (_baseClutch < ClutchHeldBelow && data.ClutchRaw > _baseClutch + ClutchRise)
                _firstInput = LaunchInput.Clutch;
            else if (data.ThrottleRaw > _baseThrottle + ThrottleRise)
                _firstInput = LaunchInput.Throttle;

            if (_firstInput != LaunchInput.None)
            {
                _reaction = (float)Math.Max(0, elapsed);

                // Rolling: already moving, so the reaction is the whole story.
                if (_technique == LaunchTechnique.Rolling)
                {
                    Finish(data, jumpStart: false, launch: 0f);
                    return;
                }
            }
        }

        if (_firstInput != LaunchInput.None && data.Speed > _baseSpeed + MoveSpeed)
        {
            Finish(data, jumpStart: false, launch: (float)Math.Max(0, elapsed));
            return;
        }

        if (elapsed > TimeoutSeconds)
        {
            if (_firstInput != LaunchInput.None)
                Finish(data, jumpStart: false, launch: 0f);
            else
                _phase = Phase.Done; // nothing to report
        }
    }

    private void Finish(TelemetryData data, bool jumpStart, float launch = 0f)
    {
        float reaction = jumpStart ? 0f : _reaction;
        if (!jumpStart && reaction > 0 && (_best <= 0 || reaction < _best))
            _best = reaction;

        Result = new RaceStartResult
        {
            ReactionSeconds = reaction,
            LaunchSeconds = launch,
            JumpStart = jumpStart,
            Technique = jumpStart ? LaunchTechnique.Unknown : _technique,
            FirstInput = jumpStart ? LaunchInput.None : _firstInput,
            GearAtGo = jumpStart ? data.Gear : _baseGear,
            SessionBestSeconds = _best,
        };
        JustMeasured = true;
        _phase = Phase.Done;
    }
}
