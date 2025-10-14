using iRacingOverlay.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.Core;

class Program
{
    static async Task Main(string[] args)
    {
        // Check for verbose/quiet mode flags
        bool verboseMode = args.Contains("--verbose") || args.Contains("-v");
        bool quietMode = args.Contains("--quiet") || args.Contains("-q");

        Console.WriteLine("===========================================");
        Console.WriteLine("iRacing Overlay - MVP 1: Basic Connection");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        if (quietMode)
        {
            Console.WriteLine("Running in QUIET mode (minimal output)");
            Console.WriteLine();
        }
        else if (verboseMode)
        {
            Console.WriteLine("Running in VERBOSE mode (detailed output)");
            Console.WriteLine();
        }

        // Build host with DI and logging
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<ITelemetryService, IRacingTelemetryService>();
                
                // Pass verbose/quiet mode to worker
                services.AddSingleton(new TelemetryDisplayOptions 
                { 
                    VerboseMode = verboseMode,
                    QuietMode = quietMode
                });
                
                services.AddHostedService<TelemetryWorker>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                
                // Reduce logging in quiet mode
                if (quietMode)
                {
                    logging.SetMinimumLevel(LogLevel.Warning);
                }
                else
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                }
            })
            .Build();

        await host.RunAsync();
    }
}

/// <summary>
/// Configuration options for telemetry display
/// </summary>
public class TelemetryDisplayOptions
{
    public bool VerboseMode { get; set; } = false;
    public bool QuietMode { get; set; } = false;
}
