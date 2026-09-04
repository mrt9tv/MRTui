using System;
using System.Threading;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Coalesces a burst of requests into a single deferred execution.
/// Used to keep synchronous disk writes off the UI thread's hot path:
/// dragging a window or a slider triggers hundreds of save requests,
/// but only one write happens once the burst settles.
///
/// The action runs on a thread-pool thread. Callers whose action touches
/// shared mutable state must synchronise it themselves.
/// </summary>
public sealed class Debouncer : IDisposable
{
    private readonly Action _action;
    private readonly int _delayMs;
    private readonly Timer _timer;
    private readonly object _gate = new();
    private bool _pending;
    private bool _disposed;

    /// <param name="action">Work to perform once the burst settles.</param>
    /// <param name="delayMs">Quiet period, in milliseconds, before the action runs.</param>
    public Debouncer(Action action, int delayMs = 750)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _delayMs = delayMs;
        _timer = new Timer(OnElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Request execution. Resets the quiet period, so a continuous stream of
    /// calls results in exactly one run after the stream stops.
    /// </summary>
    public void Trigger()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending = true;
            _timer.Change(_delayMs, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Run any pending work immediately on the calling thread and cancel the timer.
    /// Call on shutdown so nothing queued is lost.
    /// </summary>
    public void Flush()
    {
        lock (_gate)
        {
            if (_disposed || !_pending) return;
            _pending = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        Run();
    }

    private void OnElapsed(object? state)
    {
        lock (_gate)
        {
            if (_disposed || !_pending) return;
            _pending = false;
        }

        Run();
    }

    private void Run()
    {
        try
        {
            _action();
        }
        catch (Exception ex)
        {
            // A failed background save must never take the process down.
            AppLog.Error("Debounced action failed", ex);
        }
    }

    public void Dispose()
    {
        Flush();
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _timer.Dispose();
    }
}
