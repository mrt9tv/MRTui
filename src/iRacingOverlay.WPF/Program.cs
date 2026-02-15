using System;
using Velopack;

namespace iRacingOverlay.WPF;

/// <summary>
/// Application entry point for Velopack integration.
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point. Velopack update hooks must run FIRST, before the WPF app starts.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack update hooks MUST be the very first code to execute.
        // This call may restart/terminate the process without returning.
        VelopackApp.Build().Run();

        // After Velopack check, start the normal WPF application
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
