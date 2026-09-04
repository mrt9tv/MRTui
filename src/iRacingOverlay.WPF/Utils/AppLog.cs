using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Minimal rolling file log. The app ships as a WinExe with no console attached,
/// so console logging goes nowhere in Release — this is the only record a user
/// can send back when something goes wrong.
///
/// Writes to Documents/MRT-UI/logs/mrt-ui-yyyyMMdd.log on a background thread so
/// logging never blocks the UI or the telemetry thread. Files older than
/// <see cref="RetentionDays"/> are removed at startup.
/// </summary>
public static class AppLog
{
    private const int RetentionDays = 7;
    private const int MaxQueue = 4096;

    private static readonly BlockingCollection<string> _queue =
        new(new ConcurrentQueue<string>(), MaxQueue);

    private static Thread? _writer;
    private static string? _logPath;
    private static int _started;

    /// <summary>Directory holding the log files. Surfaced in the UI so users can find them.</summary>
    public static string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MRT-UI", "logs");

    /// <summary>Full path of the log file for the current session, once started.</summary>
    public static string? CurrentLogFile => _logPath;

    /// <summary>
    /// Start the background writer. Safe to call more than once; only the first call does work.
    /// </summary>
    public static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;

        try
        {
            Directory.CreateDirectory(LogDirectory);
            PruneOldLogs();

            _logPath = Path.Combine(
                LogDirectory,
                $"mrt-ui-{DateTime.Now:yyyyMMdd}.log");

            _writer = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "MRT-AppLog",
                Priority = ThreadPriority.BelowNormal,
            };
            _writer.Start();

            Info($"=== MRT UI {VersionInfo.DisplayVersion} started · {Environment.OSVersion} · .NET {Environment.Version} ===");
        }
        catch
        {
            // Logging must never prevent the app from starting.
            _logPath = null;
        }
    }

    public static void Debug(string message) => Write("DBG", message, null);
    public static void Info(string message) => Write("INF", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WRN", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERR", message, ex);

    /// <summary>Flush pending lines and stop the writer. Call on shutdown.</summary>
    public static void Shutdown()
    {
        try
        {
            if (_started == 0) return;
            Info("=== shutdown ===");
            _queue.CompleteAdding();
            _writer?.Join(1500);
        }
        catch { /* nothing useful to do while shutting down */ }
    }

    private static void Write(string level, string message, Exception? ex)
    {
        if (_started == 0 || _queue.IsAddingCompleted) return;

        var sb = new StringBuilder(160);
        sb.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
          .Append(" [").Append(level).Append("] ")
          .Append(message);

        if (ex != null)
        {
            sb.Append(Environment.NewLine)
              .Append("    ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
            if (!string.IsNullOrEmpty(ex.StackTrace))
                sb.Append(Environment.NewLine).Append(ex.StackTrace);
        }

        // Drop rather than block: a full queue means the disk is the bottleneck,
        // and stalling a 60 Hz telemetry thread to log is never the right trade.
        _queue.TryAdd(sb.ToString());
    }

    private static void WriteLoop()
    {
        try
        {
            foreach (var line in _queue.GetConsumingEnumerable())
            {
                try
                {
                    if (_logPath != null)
                        File.AppendAllText(_logPath, line + Environment.NewLine);
                }
                catch { /* disk full / locked — skip this line */ }
            }
        }
        catch (InvalidOperationException)
        {
            // CompleteAdding raced with the enumerator; nothing left to write.
        }
    }

    private static void PruneOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.GetFiles(LogDirectory, "mrt-ui-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { /* pruning is best-effort */ }
    }
}
