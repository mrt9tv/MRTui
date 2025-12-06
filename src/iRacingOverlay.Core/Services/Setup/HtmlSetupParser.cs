using System.Text.RegularExpressions;

namespace iRacingOverlay.Core.Services.Setup;

/// <summary>
/// Parser for iRacing HTML setup files (.htm)
/// Extracts setup parameters from HTML exported by iRacing Garage
/// </summary>
public class HtmlSetupParser
{
    /// <summary>
    /// Parse iRacing HTML setup file into structured data
    /// </summary>
    public SetupConfiguration ParseHtmlFile(string filePath)
    {
        var html = File.ReadAllText(filePath);
        var setupName = Path.GetFileNameWithoutExtension(filePath);
        
        var setup = new SetupConfiguration
        {
            SetupId = Guid.NewGuid().ToString(),
            SetupName = setupName,
            Timestamp = DateTime.UtcNow
        };
        
        // Extract car and track from title
        var titleMatch = Regex.Match(html, @"<H2[^>]*>.*?<br>\s*([^<]+)\s+setup:\s*([^<]+)<br>\s*track:\s*([^<]+)</H2>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleMatch.Success)
        {
            var carName = titleMatch.Groups[1].Value.Trim();
            var rawSetupName = titleMatch.Groups[2].Value.Trim();
            var trackName = titleMatch.Groups[3].Value.Trim();
            
            setup.SetupName = $"{setupName} ({carName} @ {trackName})";
        }
        
        // Parse all setup parameters
        var parameters = new Dictionary<string, object>();
        
        // Tire pressures (kPa)
        ExtractTireParameter(html, parameters, "LEFT FRONT", "Cold pressure:");
        ExtractTireParameter(html, parameters, "RIGHT FRONT", "Cold pressure:");
        ExtractTireParameter(html, parameters, "LEFT REAR", "Cold pressure:");
        ExtractTireParameter(html, parameters, "RIGHT REAR", "Cold pressure:");
        
        // Aero
        ExtractParameter(html, parameters, "AERO", "Front mainplane angle:", "Aero_FrontWing");
        ExtractParameter(html, parameters, "AERO", "Rear wing angle:", "Aero_RearWing");
        ExtractParameter(html, parameters, "AERO CALCULATOR", "Aero balance:", "Aero_Balance");
        
        // Front
        ExtractParameter(html, parameters, "FRONT:", "ARB blade:", "Front_ARB");
        ExtractParameter(html, parameters, "FRONT:", "Brake pressure bias:", "Front_BrakeBias");
        ExtractParameter(html, parameters, "FRONT:", "Cross weight:", "Chassis_CrossWeight");
        
        // Left Front
        ExtractParameter(html, parameters, "LEFT FRONT:", "Ride height:", "LF_RideHeight");
        ExtractParameter(html, parameters, "LEFT FRONT:", "Spring rate:", "LF_SpringRate");
        ExtractParameter(html, parameters, "LEFT FRONT:", "Camber:", "LF_Camber");
        ExtractParameter(html, parameters, "LEFT FRONT:", "Toe-in:", "LF_Toe");
        
        // Right Front
        ExtractParameter(html, parameters, "RIGHT FRONT:", "Ride height:", "RF_RideHeight");
        ExtractParameter(html, parameters, "RIGHT FRONT:", "Spring rate:", "RF_SpringRate");
        ExtractParameter(html, parameters, "RIGHT FRONT:", "Camber:", "RF_Camber");
        ExtractParameter(html, parameters, "RIGHT FRONT:", "Toe-in:", "RF_Toe");
        
        // Rear
        ExtractParameter(html, parameters, "REAR:", "Ride height:", "Rear_RideHeight");
        ExtractParameter(html, parameters, "REAR:", "ARB blade:", "Rear_ARB");
        ExtractParameter(html, parameters, "REAR:", "Fuel level:", "Chassis_FuelLevel");
        
        // Left Rear
        ExtractParameter(html, parameters, "LEFT REAR:", "Spring rate:", "LR_SpringRate");
        ExtractParameter(html, parameters, "LEFT REAR:", "Camber:", "LR_Camber");
        ExtractParameter(html, parameters, "LEFT REAR:", "Toe-in:", "LR_Toe");
        
        // Right Rear
        ExtractParameter(html, parameters, "RIGHT REAR:", "Spring rate:", "RR_SpringRate");
        ExtractParameter(html, parameters, "RIGHT REAR:", "Camber:", "RR_Camber");
        ExtractParameter(html, parameters, "RIGHT REAR:", "Toe-in:", "RR_Toe");
        
        // Serialize to JSON
        setup.SetupData = System.Text.Json.JsonSerializer.Serialize(parameters, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        return setup;
    }
    
    private void ExtractParameter(string html, Dictionary<string, object> parameters, string section, string paramName, string? keyOverride = null)
    {
        try
        {
            var sectionRegex = $@"<H2><U>{Regex.Escape(section)}</U></H2>(.*?)(?=<H2>|</body>|$)";
            var sectionMatch = Regex.Match(html, sectionRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (sectionMatch.Success)
            {
                var sectionContent = sectionMatch.Groups[1].Value;
                var paramRegex = $@"{Regex.Escape(paramName)}\s*<U>([^<]+)</U>";
                var paramMatch = Regex.Match(sectionContent, paramRegex, RegexOptions.IgnoreCase);
                
                if (paramMatch.Success)
                {
                    var value = paramMatch.Groups[1].Value.Trim();
                    var key = keyOverride ?? paramName.Replace(":", "").Trim();
                    
                    // Try to parse numeric values
                    if (float.TryParse(Regex.Match(value, @"-?\d+\.?\d*").Value, out var numericValue))
                    {
                        parameters[key] = numericValue;
                    }
                    else
                    {
                        parameters[key] = value;
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors for individual parameters
        }
    }
    
    private void ExtractTireParameter(string html, Dictionary<string, object> parameters, string tireName, string paramName)
    {
        var key = $"{tireName.Replace(" ", "")}_ColdPressure";
        ExtractParameter(html, parameters, tireName, paramName, key);
    }
    
    /// <summary>
    /// Generate human-readable summary of setup from HTML
    /// </summary>
    public string GenerateSetupSummary(string filePath)
    {
        var setup = ParseHtmlFile(filePath);
        if (string.IsNullOrEmpty(setup.SetupData)) return "No setup data";
        
        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(setup.SetupData);
        
        if (data == null) return "No setup data";
        
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"📋 {setup.SetupName}");
        summary.AppendLine();
        
        // Aero
        if (data.ContainsKey("Aero_FrontWing") || data.ContainsKey("Aero_RearWing"))
        {
            summary.AppendLine("✈️ AERO:");
            if (data.ContainsKey("Aero_FrontWing")) 
                summary.AppendLine($"  Front Wing: {data["Aero_FrontWing"]}");
            if (data.ContainsKey("Aero_RearWing")) 
                summary.AppendLine($"  Rear Wing: {data["Aero_RearWing"]}");
            if (data.ContainsKey("Aero_Balance")) 
                summary.AppendLine($"  Balance: {data["Aero_Balance"]}");
            summary.AppendLine();
        }
        
        // Tires
        summary.AppendLine("🏁 TIRES (Cold Pressure):");
        if (data.ContainsKey("LEFTFRONT_ColdPressure")) 
            summary.AppendLine($"  LF: {data["LEFTFRONT_ColdPressure"]}");
        if (data.ContainsKey("RIGHTFRONT_ColdPressure")) 
            summary.AppendLine($"  RF: {data["RIGHTFRONT_ColdPressure"]}");
        if (data.ContainsKey("LEFTREAR_ColdPressure")) 
            summary.AppendLine($"  LR: {data["LEFTREAR_ColdPressure"]}");
        if (data.ContainsKey("RIGHTREAR_ColdPressure")) 
            summary.AppendLine($"  RR: {data["RIGHTREAR_ColdPressure"]}");
        summary.AppendLine();
        
        // Chassis
        if (data.ContainsKey("Front_ARB") || data.ContainsKey("Rear_ARB"))
        {
            summary.AppendLine("🔧 CHASSIS:");
            if (data.ContainsKey("Front_ARB")) 
                summary.AppendLine($"  Front ARB: {data["Front_ARB"]}");
            if (data.ContainsKey("Rear_ARB")) 
                summary.AppendLine($"  Rear ARB: {data["Rear_ARB"]}");
            if (data.ContainsKey("Chassis_FuelLevel")) 
                summary.AppendLine($"  Fuel: {data["Chassis_FuelLevel"]}");
        }
        
        return summary.ToString();
    }
}
