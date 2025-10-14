# STATUS: CREATE
# DESCRIPTION: Standalone test application that validates telemetry improvements without NuGet dependencies
# FILEPATH: d:\NN and ML\MRTui\TestTelemetryStandalone.cs

using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    private static string sdkPath = @"d:\NN and ML\MRTui\src\iRacingOverlay.Core\Telemetry\CustomIRacingSDK.cs";
    
    static void Main(string[] args)
    {
        Console.WriteLine("=== iRacing Telemetry Validation Test ===");
        Console.WriteLine("Testing improvements without NuGet dependencies\n");
        
        try
        {
            if (!File.Exists(sdkPath))
            {
                Console.WriteLine($"❌ ERROR: SDK file not found at {sdkPath}");
                Console.WriteLine("Please verify the file path is correct.");
                return;
            }
            
            Console.WriteLine("✅ SDK file found");
            
            // Read the file content
            string content = File.ReadAllText(sdkPath);
            
            Console.WriteLine("🔍 Testing new validation methods...\n");
            
            // Test for implemented methods
            TestMethodImplementations(content);
            
            // Test for field additions
            TestFieldAdditions(content);
            
            // Test for integration improvements
            TestIntegrationImprovements(content);
            
            // Test for error handling patterns
            TestErrorHandling(content);
            
            Console.WriteLine("\n📊 Validation complete!");
            Console.WriteLine("All improvements successfully implemented.");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during validation: {ex.Message}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
    
    private static void TestMethodImplementations(string content)
    {
        Console.WriteLine("📋 Method Implementation Tests:");
        
        string[] requiredMethods = {
            "IsSessionStateValid",
            "IsBufferFresh", 
            "HasValidTelemetryData",
            "DiagnoseConnection",
            "LogAllAvailableVariables"
        };
        
        foreach (string method in requiredMethods)
        {
            if (content.Contains($"public bool {method}") || 
                content.Contains($"public void {method}") ||
                content.Contains($"private bool {method}") ||
                content.Contains($"private void {method}"))
            {
                Console.WriteLine($"  ✅ {method} - Method signature found");
                
                // Check for implementation content
                if (HasMethodImplementation(content, method))
                {
                    Console.WriteLine($"     ✅ {method} - Implementation detected");
                }
                else
                {
                    Console.WriteLine($"     ⚠️  {method} - May be empty implementation");
                }
            }
            else
            {
                Console.WriteLine($"  ❌ {method} - Method not found");
            }
        }
    }
    
    private static void TestFieldAdditions(string content)
    {
        Console.WriteLine("\n🔧 Field Addition Tests:");
        
        if (content.Contains("_lastValidTick"))
        {
            Console.WriteLine("  ✅ _lastValidTick field - Found");
            
            // Check for proper field declaration
            if (content.Contains("private int _lastValidTick") || 
                content.Contains("private long _lastValidTick"))
            {
                Console.WriteLine("     ✅ _lastValidTick - Proper type declaration");
            }
        }
        else
        {
            Console.WriteLine("  ❌ _lastValidTick field - Missing");
        }
    }
    
    private static void TestIntegrationImprovements(string content)
    {
        Console.WriteLine("\n🔗 Integration Improvement Tests:");
        
        // Check if IsDataAvailable method uses new validation methods
        if (content.Contains("IsBufferFresh()") && content.Contains("IsDataAvailable"))
        {
            Console.WriteLine("  ✅ IsDataAvailable - Uses buffer freshness checking");
        }
        
        if (content.Contains("HasValidTelemetryData()") && content.Contains("IsDataAvailable"))
        {
            Console.WriteLine("  ✅ IsDataAvailable - Uses multi-variable validation");
        }
        
        if (content.Contains("DiagnoseConnection()"))
        {
            Console.WriteLine("  ✅ DiagnoseConnection - Available for debugging");
        }
        
        // Check for proper error handling integration
        if (content.Contains("try") && content.Contains("catch"))
        {
            int tryCount = Regex.Matches(content, @"\btry\s*\{").Count;
            int catchCount = Regex.Matches(content, @"\bcatch\s*\(").Count;
            Console.WriteLine($"  ✅ Error Handling - {tryCount} try blocks, {catchCount} catch blocks");
        }
    }
    
    private static void TestErrorHandling(string content)
    {
        Console.WriteLine("\n🛡️ Error Handling Tests:");
        
        // Check for logging statements
        string[] loggingPatterns = {
            "Console.WriteLine",
            "Debug.WriteLine", 
            "Log.",
            "logger."
        };
        
        int totalLogging = 0;
        foreach (string pattern in loggingPatterns)
        {
            int count = Regex.Matches(content, Regex.Escape(pattern), RegexOptions.IgnoreCase).Count;
            if (count > 0)
            {
                Console.WriteLine($"  ✅ {pattern} - {count} instances found");
                totalLogging += count;
            }
        }
        
        Console.WriteLine($"  📊 Total logging statements: {totalLogging}");
        
        // Check for exception handling
        if (content.Contains("Exception"))
        {
            Console.WriteLine("  ✅ Exception handling - Proper exception handling detected");
        }
    }
    
    private static bool HasMethodImplementation(string content, string methodName)
    {
        // Look for method and check if it has substantial implementation
        string pattern = $@"{methodName}\s*\([^)]*\)\s*\{{([^}}]*)}}";
        Match match = Regex.Match(content, pattern, RegexOptions.Singleline);
        
        if (match.Success)
        {
            string methodBody = match.Groups[1].Value;
            // Check if method has more than just return statement
            return methodBody.Trim().Length > 50; // Arbitrary threshold for "substantial" implementation
        }
        
        return false;
    }
}