using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using SVappsLAB.iRacingTelemetrySDK;

namespace iRacingOverlay.Core.Services;

/// <summary>
/// Diagnostic tool to dump all available telemetry variables from the iRacing SDK
/// This helps discover the correct variable names when documentation is incomplete
/// </summary>
public class TelemetryVariableDumper
{
    private readonly ILogger<TelemetryVariableDumper> _logger;

    public TelemetryVariableDumper(ILogger<TelemetryVariableDumper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Dump all available variables from a connected telemetry client
    /// </summary>
    public void DumpAllVariables(object telemetryClient, string outputPath)
    {
        try
        {
            _logger.LogInformation("Starting variable dump to: {OutputPath}", outputPath);

            var clientType = telemetryClient.GetType();

            // Try to get the data provider through reflection
            var dataProviderField = clientType.GetField("_dataProvider", BindingFlags.NonPublic | BindingFlags.Instance);
            if (dataProviderField == null)
            {
                _logger.LogError("Could not find _dataProvider field on telemetry client");
                return;
            }

            var dataProvider = dataProviderField.GetValue(telemetryClient);
            if (dataProvider == null)
            {
                _logger.LogError("Data provider is null");
                return;
            }

            var dataProviderType = dataProvider.GetType();
            _logger.LogInformation("Data provider type: {TypeName}", dataProviderType.FullName);

            // Try to call GetVarHeaders() method
            var getVarHeadersMethod = dataProviderType.GetMethod("GetVarHeaders");
            if (getVarHeadersMethod != null)
            {
                try
                {
                    var varHeaders = getVarHeadersMethod.Invoke(dataProvider, null);
                    _logger.LogInformation("Successfully called GetVarHeaders()");
                    if (varHeaders != null)
                    {
                        DumpVarHeaders(varHeaders, outputPath);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to call GetVarHeaders(): {Message}", ex.Message);
                }
            }

            // Fallback: Try to get the variable header or data map
            var varHeadersField = dataProviderType.GetField("_varHeaders", BindingFlags.NonPublic | BindingFlags.Instance);
            var varBufferField = dataProviderType.GetField("_varBuffer", BindingFlags.NonPublic | BindingFlags.Instance);

            if (varHeadersField != null)
            {
                var varHeaders = varHeadersField.GetValue(dataProvider);
                _logger.LogInformation("Found _varHeaders field");
                if (varHeaders != null)
                {
                    DumpVarHeaders(varHeaders, outputPath);
                }
            }
            else if (varBufferField != null)
            {
                var varBuffer = varBufferField.GetValue(dataProvider);
                _logger.LogInformation("Found _varBuffer field");
                if (varBuffer != null)
                {
                    DumpVarBuffer(varBuffer, outputPath);
                }
            }
            else
            {
                // Try to enumerate all fields and properties
                _logger.LogWarning("Could not find var headers or buffer, enumerating all members...");
                DumpAllMembers(dataProvider, outputPath);
            }

            _logger.LogInformation("Variable dump complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dumping variables: {Message}", ex.Message);
        }
    }

    private void DumpVarHeaders(object varHeaders, string outputPath)
    {
        if (varHeaders == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("# iRacing SDK Available Variables");
        sb.AppendLine($"# Generated: {DateTime.Now}");
        sb.AppendLine($"# Source: {varHeaders.GetType().FullName}");
        sb.AppendLine();

        // If it's a dictionary or collection, enumerate it
        if (varHeaders is System.Collections.IDictionary dict)
        {
            sb.AppendLine($"# Total variables: {dict.Count}");
            sb.AppendLine();

            var sortedKeys = dict.Keys.Cast<object>().OrderBy(k => k.ToString()).ToList();

            foreach (var key in sortedKeys)
            {
                var value = dict[key];
                sb.AppendLine($"{key} = {value}");
            }
        }
        else if (varHeaders is System.Collections.IEnumerable enumerable)
        {
            int count = 0;
            foreach (var item in enumerable)
            {
                sb.AppendLine($"[{count}] {item}");
                count++;
            }
            sb.AppendLine();
            sb.AppendLine($"# Total variables: {count}");
        }
        else
        {
            sb.AppendLine($"# Unknown type: {varHeaders.GetType().FullName}");
        }

        File.WriteAllText(outputPath, sb.ToString());
        _logger.LogInformation("Wrote {LineCount} lines to {OutputPath}", sb.ToString().Split('\n').Length, outputPath);
    }

    private void DumpVarBuffer(object varBuffer, string outputPath)
    {
        // Similar approach for var buffer
        DumpVarHeaders(varBuffer, outputPath);
    }

    private void DumpAllMembers(object dataProvider, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# iRacing SDK Data Provider Members");
        sb.AppendLine($"# Generated: {DateTime.Now}");
        sb.AppendLine($"# Type: {dataProvider.GetType().FullName}");
        sb.AppendLine();

        var type = dataProvider.GetType();

        sb.AppendLine("## Fields:");
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            sb.AppendLine($"  {field.FieldType.Name} {field.Name}");
        }

        sb.AppendLine();
        sb.AppendLine("## Properties:");
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            sb.AppendLine($"  {prop.PropertyType.Name} {prop.Name}");
        }

        sb.AppendLine();
        sb.AppendLine("## Methods:");
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")))
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            sb.AppendLine($"  {method.ReturnType.Name} {method.Name}({parameters})");
        }

        File.WriteAllText(outputPath, sb.ToString());
        _logger.LogInformation("Wrote member dump to {OutputPath}", outputPath);
    }

    /// <summary>
    /// Simplified version that uses SDK's GetRawTelemetryVarHeaders method if available
    /// </summary>
    public void DumpUsingSDKMethod(object telemetryClient, string outputPath)
    {
        try
        {
            _logger.LogInformation("Attempting to dump using SDK method");

            var clientType = telemetryClient.GetType();

            // Look for GetRawTelemetryVarHeaders or similar methods
            var methods = clientType.GetMethods()
                .Where(m => m.Name.Contains("Var") || m.Name.Contains("Header") || m.Name.Contains("Dump"))
                .ToList();

            _logger.LogInformation("Found {Count} potential methods", methods.Count);

            var sb = new StringBuilder();
            sb.AppendLine("# Available SDK Methods");
            sb.AppendLine($"# Client Type: {clientType.FullName}");
            sb.AppendLine();

            foreach (var method in methods)
            {
                sb.AppendLine($"{method.ReturnType.Name} {method.Name}()");

                // Try to invoke if it's a parameter-less method
                if (method.GetParameters().Length == 0 && method.ReturnType != typeof(void))
                {
                    try
                    {
                        var result = method.Invoke(telemetryClient, null);
                        if (result != null)
                        {
                            sb.AppendLine($"  Result type: {result.GetType().FullName}");
                            sb.AppendLine($"  Result: {result}");
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  Error invoking: {ex.Message}");
                    }
                }
            }

            File.WriteAllText(outputPath, sb.ToString());
            _logger.LogInformation("Wrote SDK method dump to {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DumpUsingSDKMethod: {Message}", ex.Message);
        }
    }
}
