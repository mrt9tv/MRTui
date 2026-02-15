using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF.Pages;

public partial class AboutPage : UserControl
{
    private UpdateService? _updateService;

    public AboutPage()
    {
        InitializeComponent();
        TxtVersion.Text = VersionInfo.DisplayVersion;
    }

    /// <summary>
    /// Inject the UpdateService after construction (called from MainWindow).
    /// </summary>
    public void SetUpdateService(UpdateService updateService)
    {
        _updateService = updateService;

        // If not installed via Velopack (dev build), show that
        if (!_updateService.IsInstalled)
        {
            TxtUpdateInfo.Text = "Running from development build — auto-update disabled.";
        }
    }

    private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_updateService == null || !_updateService.IsInstalled)
        {
            TxtUpdateStatus.Text = "Not available";
            TxtUpdateInfo.Text = "Auto-update only works when installed via the Velopack installer.";
            return;
        }

        BtnCheckUpdate.IsEnabled = false;
        TxtUpdateStatus.Text = "Checking...";
        TxtUpdateInfo.Text = "";

        try
        {
            bool hasUpdate = await _updateService.CheckForUpdatesAsync();

            if (hasUpdate)
            {
                TxtUpdateStatus.Text = $"Update available: v{_updateService.LatestVersion}";
                TxtUpdateInfo.Text = "Downloading...";

                await _updateService.DownloadUpdateAsync(progress =>
                {
                    Dispatcher.Invoke(() => TxtUpdateInfo.Text = $"Downloading... {progress}%");
                });

                TxtUpdateInfo.Text = "Download complete. Restarting...";
                _updateService.ApplyUpdateAndRestart();
            }
            else
            {
                TxtUpdateStatus.Text = "You're up to date!";
                TxtUpdateInfo.Text = $"Current version: {VersionInfo.DisplayVersion}";
            }
        }
        catch (Exception ex)
        {
            TxtUpdateStatus.Text = "Update check failed";
            TxtUpdateInfo.Text = ex.Message;
        }
        finally
        {
            BtnCheckUpdate.IsEnabled = true;
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
