using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Writes the variables the CURRENT session actually publishes, with iRacing's own
/// description and unit for each.
///
/// This exists because the SDK's <c>TelemetryVar</c> enum is a superset: it also
/// covers channels that are only written into disk-logged .ibt files and never
/// reach live shared memory. Designing against the enum led to two wrong
/// conclusions — that per-wheel speeds were available for lockup detection, and
/// that GPS coordinates could drive a track map. Neither is live.
///
/// The dump is the authority. It also carries the Desc field, which settles what a
/// channel actually means rather than what its name suggests: ShiftPowerPct, for
/// instance, is not a reading of engine power.
/// </summary>
public static class TelemetryVariableDump
{
    /// <summary>Where the dump is written.</summary>
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "MRT-UI", "available_variables.txt");

    /// <summary>
    /// Write the SDK's variable list out. Since SDK 2.x <c>GetTelemetryVariables()</c>
    /// is on the client interface, so this no longer needs reflection to reach it.
    /// </summary>
    /// <returns>Number of variables written, or -1 on failure.</returns>
    public static int Write(IEnumerable variables)
    {
        try
        {
            var rows = variables.Cast<object>().Select(Describe)
                                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                                .ToList();

            if (rows.Count == 0) return 0;

            var sb = new StringBuilder();
            sb.AppendLine("# iRacing telemetry variables present in this session");
            sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"# Count: {rows.Count}");
            sb.AppendLine("#");
            sb.AppendLine("# This is the LIVE set. The SDK's TelemetryVar enum is larger because it");
            sb.AppendLine("# also lists disk-logging (.ibt) channels that never reach shared memory.");
            sb.AppendLine("# Check a channel here before building anything on it.");
            sb.AppendLine();
            sb.AppendLine($"{"NAME",-34} {"TYPE",-10} {"UNITS",-14} DESCRIPTION");
            sb.AppendLine(new string('-', 110));

            foreach (var r in rows)
                sb.AppendLine($"{r.Name,-34} {r.Type,-10} {r.Units,-14} {r.Desc}");

            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, sb.ToString());

            return rows.Count;
        }
        catch
        {
            // Diagnostics must never affect the session.
            return -1;
        }
    }

    private static (string Name, string Type, string Units, string Desc) Describe(object variable)
    {
        var t = variable.GetType();

        string Read(string property)
        {
            var value = t.GetProperty(property)?.GetValue(variable);
            return value?.ToString()?.Trim() ?? "";
        }

        var length = Read("Length");
        var type = Read("Type");
        if (length is not ("" or "1")) type += $"[{length}]";

        return (Read("Name"), type, Read("Units"), Read("Desc"));
    }
}
