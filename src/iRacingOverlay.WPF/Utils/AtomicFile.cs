using System;
using System.IO;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Crash-safe text file writes.
///
/// <c>File.WriteAllText</c> truncates before it writes, so a crash or power loss
/// mid-write leaves a zero-length or half-written config and the loader silently
/// falls back to defaults — losing the user's whole layout. Writing to a temp file
/// and swapping it into place makes the update atomic, and keeps the previous
/// good copy as a <c>.bak</c> to fall back on.
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// Write <paramref name="contents"/> to <paramref name="path"/> atomically,
    /// preserving the previous contents as "<paramref name="path"/>.bak".
    /// </summary>
    public static void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        var backup = path + ".bak";

        File.WriteAllText(temp, contents);

        if (File.Exists(path))
        {
            // Replace swaps temp into place and rolls the old file into the backup
            // in one operation the filesystem will not leave half-done.
            File.Replace(temp, path, backup, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temp, path);
        }
    }

    /// <summary>
    /// Read a file, falling back to its ".bak" if the primary is missing or empty.
    /// Returns null when neither is usable.
    /// </summary>
    public static string? ReadAllTextWithFallback(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(text)) return text;
                AppLog.Warn($"'{Path.GetFileName(path)}' was empty — trying backup");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Could not read '{Path.GetFileName(path)}' — trying backup", ex);
        }

        try
        {
            var backup = path + ".bak";
            if (File.Exists(backup))
            {
                var text = File.ReadAllText(backup);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    AppLog.Info($"Recovered '{Path.GetFileName(path)}' from backup");
                    return text;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Backup for '{Path.GetFileName(path)}' unreadable", ex);
        }

        return null;
    }
}
