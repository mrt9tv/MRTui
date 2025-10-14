using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using iRacingOverlay.Core.Telemetry;

namespace SimpleConnectionTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Simple Connection Test ===");
            
            // Create logger
            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            var logger = loggerFactory.CreateLogger<CustomIRacingSDK>();
            
            // Create SDK instance and test connection
            var sdk = new CustomIRacingSDK(logger);
            
            Console.WriteLine("Attempting to connect...");
            bool connected = sdk.Connect();
            
            Console.WriteLine($"Connection result: {connected}");
            Console.WriteLine($"IsConnected property: {sdk.IsConnected}");
            
            if (connected)
            {
                Console.WriteLine("✅ SUCCESS: Connected to iRacing");
                
                // Try to read some data
                try
                {
                    Console.WriteLine("Testing data access...");
                    sdk.RunPhase1MemoryAnalysis();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error accessing data: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("❌ FAILED: Could not connect to iRacing");
            }
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
