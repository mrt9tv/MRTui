using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Leaves evidence when the UI thread stops responding.
///
/// A pool timer posts a probe to the dispatcher every quarter second and measures
/// how long the probe takes to run. A stall that clears is logged once, with its
/// duration, from the UI thread when the probe finally lands. A stall that does
/// not clear is logged from the timer thread instead, so a genuine deadlock still
/// leaves a line in the file. Nothing here touches the UI beyond the empty probe.
/// </summary>
public sealed class UiWatchdog : IDisposable
{
    private const int ProbeIntervalMs = 250;
    private const int StallThresholdMs = 500;
    private const int HungThresholdMs = 5000;

    private readonly Dispatcher _dispatcher;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Timer _timer;

    /// <summary>Clock reading when the in-flight probe was posted; -1 when none is pending.</summary>
    private long _probeSentAt = -1;

    /// <summary>Set once the hung warning has been written for the current stall.</summary>
    private int _hungReported;

    public UiWatchdog(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _timer = new Timer(Tick, null, ProbeIntervalMs, ProbeIntervalMs);
    }

    private void Tick(object? _)
    {
        long sentAt = Interlocked.Read(ref _probeSentAt);

        if (sentAt >= 0)
        {
            // The last probe has not run yet. Report from here if it has been
            // long enough that the UI thread may never come back.
            long waiting = _clock.ElapsedMilliseconds - sentAt;
            if (waiting >= HungThresholdMs && Interlocked.CompareExchange(ref _hungReported, 1, 0) == 0)
                AppLog.Error($"UI thread has not responded for {waiting / 1000.0:F1} s");
            return;
        }

        Interlocked.Exchange(ref _probeSentAt, _clock.ElapsedMilliseconds);

        try
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, OnProbe);
        }
        catch (Exception ex)
        {
            // Dispatcher shut down underneath us — nothing left to watch.
            Interlocked.Exchange(ref _probeSentAt, -1);
            AppLog.Warn("UI watchdog stopped", ex);
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void OnProbe()
    {
        long sentAt = Interlocked.Exchange(ref _probeSentAt, -1);
        if (sentAt < 0) return;

        long latency = _clock.ElapsedMilliseconds - sentAt;
        bool wasHung = Interlocked.Exchange(ref _hungReported, 0) == 1;

        if (wasHung)
            AppLog.Warn($"UI thread recovered after {latency / 1000.0:F1} s");
        else if (latency >= StallThresholdMs)
            AppLog.Warn($"UI thread stalled for {latency} ms");
    }

    public void Dispose() => _timer.Dispose();
}
