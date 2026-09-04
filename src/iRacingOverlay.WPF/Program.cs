using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Velopack;

namespace iRacingOverlay.WPF;

/// <summary>
/// Application entry point for Velopack integration.
/// </summary>
public static class Program
{
    /// <summary>
    /// Machine-wide name so a second launch can detect the first, whichever
    /// directory it was started from.
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\MRT-UI-SingleInstance";

    /// <summary>Custom message used to ask an already-running instance to show itself.</summary>
    private const string ShowExistingMessage = "MRT_UI_SHOW_EXISTING_WINDOW";

    private static Mutex? _singleInstanceMutex;

    /// <summary>
    /// Broadcast message id the running instance listens for. Registered once here
    /// and again in MainWindow so both agree on the value.
    /// </summary>
    internal static readonly uint ShowExistingWindowMessage =
        RegisterWindowMessage(ShowExistingMessage);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    /// <summary>
    /// Main entry point. Velopack update hooks must run FIRST, before the WPF app starts.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // Force invariant culture so all numeric formatting uses '.' as decimal separator,
        // regardless of the user's Windows regional settings.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        // Velopack update hooks MUST be the very first code to execute.
        // This call may restart/terminate the process without returning.
        VelopackApp.Build().Run();

        // A second copy would create a duplicate set of overlays, fail to register
        // the global hotkeys (they are already owned by the first instance), and
        // race the first over layout.json. Hand focus back to the running copy instead.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            PostMessage(HWND_BROADCAST, ShowExistingWindowMessage, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        try
        {
            // After Velopack check, start the normal WPF application
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        finally
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }
    }
}
