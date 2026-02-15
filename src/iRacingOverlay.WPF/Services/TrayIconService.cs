using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace iRacingOverlay.WPF.Services;

/// <summary>
/// Manages the system tray icon for minimize-to-tray behavior.
/// Uses H.NotifyIcon.Wpf — pure WPF, no WinForms dependency.
/// Creates a programmatic teal "9" icon (brand: MRT#9).
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private bool _disposed;

    public event EventHandler? RestoreRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_trayIcon != null) return;

        _trayIcon = new TaskbarIcon
        {
            Icon = CreateTemporaryIcon(),
            ToolTipText = "MRT UI — iRacing Overlay",
        };

        // Context menu
        var menu = new System.Windows.Controls.ContextMenu();
        var showItem = new System.Windows.Controls.MenuItem { Header = "Show MRT UI" };
        showItem.Click += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(showItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) =>
        {
            // Close context menu immediately to prevent lingering selection bar artifact
            menu.IsOpen = false;
            ExitRequested?.Invoke(this, EventArgs.Empty);
        };
        menu.Items.Add(exitItem);
        _trayIcon.ContextMenu = menu;

        // Double-click restores
        _trayIcon.TrayLeftMouseDown += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);

        _trayIcon.ForceCreate();
    }

    /// <summary>Show the tray icon (call when minimizing to tray).</summary>
    public void Show()
    {
        if (_trayIcon != null)
            _trayIcon.Visibility = Visibility.Visible;
    }

    /// <summary>Hide the tray icon (call when restoring from tray).</summary>
    public void Hide()
    {
        if (_trayIcon != null)
            _trayIcon.Visibility = Visibility.Collapsed;
    }

    /// <summary>Show a balloon tooltip notification.</summary>
    public void ShowBalloon(string title, string text)
    {
        _trayIcon?.ShowNotification(title, text);
    }

    /// <summary>
    /// Creates a 32×32 icon with a teal "9" on a dark background.
    /// </summary>
    private static System.Drawing.Icon CreateTemporaryIcon()
    {
        const int size = 32;
        var renderTarget = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);

        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            // Dark background
            ctx.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                null, new Rect(0, 0, size, size));

            // Teal "9"
            var formattedText = new FormattedText(
                "9",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                22,
                new SolidColorBrush(Color.FromRgb(0, 188, 212)),
                96);

            double x = (size - formattedText.Width) / 2;
            double y = (size - formattedText.Height) / 2;
            ctx.DrawText(formattedText, new Point(x, y));
        }

        renderTarget.Render(visual);

        // Convert WPF bitmap to System.Drawing.Icon via PNG stream
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderTarget));
        using var pngStream = new System.IO.MemoryStream();
        encoder.Save(pngStream);
        pngStream.Position = 0;

        using var bitmap = new System.Drawing.Bitmap(pngStream);
        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_trayIcon != null)
        {
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
