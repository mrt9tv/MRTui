using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace iRacingOverlay.WPF.Services;

/// <summary>
/// Manages application updates via Velopack + GitHub Releases.
/// Source: https://github.com/mrt9tv/MRTui
/// </summary>
public sealed class UpdateService
{
    private const string GITHUB_REPO_URL = "https://github.com/mrt9tv/MRTui";

    private readonly UpdateManager _updateManager;
    private readonly ILogger<UpdateService>? _logger;
    private UpdateInfo? _latestUpdate;

    public UpdateService(ILogger<UpdateService>? logger = null)
    {
        _logger = logger;
        var source = new GithubSource(GITHUB_REPO_URL, null, false);
        _updateManager = new UpdateManager(source);
    }

    /// <summary>Whether the app was installed via Velopack (vs running from dev build).</summary>
    public bool IsInstalled => _updateManager.IsInstalled;

    /// <summary>The current installed version, or null if not installed via Velopack.</summary>
    public string? CurrentVersion => _updateManager.CurrentVersion?.ToFullString();

    /// <summary>Whether an update has been downloaded and is ready to apply.</summary>
    public bool IsUpdateReady => _latestUpdate != null;

    /// <summary>The version of the latest available update, or null.</summary>
    public string? LatestVersion => _latestUpdate?.TargetFullRelease?.Version?.ToFullString();

    /// <summary>
    /// Check GitHub Releases for a newer version.
    /// Returns true if an update is available.
    /// </summary>
    public async Task<bool> CheckForUpdatesAsync()
    {
        if (!IsInstalled)
        {
            _logger?.LogDebug("Not installed via Velopack — skipping update check");
            return false;
        }

        try
        {
            _logger?.LogInformation("Checking for updates...");
            _latestUpdate = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

            if (_latestUpdate != null)
            {
                _logger?.LogInformation("Update available: {Version}", LatestVersion);
                return true;
            }

            _logger?.LogInformation("No updates available");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check for updates");
            return false;
        }
    }

    /// <summary>
    /// Download the latest update. Call after CheckForUpdatesAsync returns true.
    /// </summary>
    public async Task DownloadUpdateAsync(Action<int>? progress = null)
    {
        if (_latestUpdate == null) return;

        try
        {
            _logger?.LogInformation("Downloading update {Version}...", LatestVersion);
            await _updateManager.DownloadUpdatesAsync(_latestUpdate, progress).ConfigureAwait(false);
            _logger?.LogInformation("Update downloaded successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to download update");
            throw;
        }
    }

    /// <summary>
    /// Apply the downloaded update and restart the application.
    /// </summary>
    public void ApplyUpdateAndRestart()
    {
        if (_latestUpdate == null) return;

        try
        {
            _logger?.LogInformation("Applying update and restarting...");
            _updateManager.ApplyUpdatesAndRestart(_latestUpdate);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply update");
            throw;
        }
    }

    /// <summary>
    /// One-shot: check, download, and apply update (with optional progress callback).
    /// Returns true if an update was found and applied (app will restart).
    /// Returns false if no update available.
    /// </summary>
    public async Task<bool> CheckDownloadAndApplyAsync(Action<int>? progress = null)
    {
        bool hasUpdate = await CheckForUpdatesAsync().ConfigureAwait(false);
        if (!hasUpdate) return false;

        await DownloadUpdateAsync(progress).ConfigureAwait(false);
        ApplyUpdateAndRestart();
        return true; // Won't reach here if restart succeeds
    }
}
