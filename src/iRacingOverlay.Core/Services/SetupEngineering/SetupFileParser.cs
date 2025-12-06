using System.Xml;
using System.Text.Json;
using iRacingOverlay.Core.Services.Setup;

namespace iRacingOverlay.Core.Services.SetupEngineering;

/// <summary>
/// iRacing Setup File (.sto) Parser
/// Parses iRacing XML setup files into structured data
/// 
/// iRacing .sto files are XML with structure:
/// <iRacingSetup version="1.0">
///   <Chassis>
///     <FrontARB>5</FrontARB>
///     <RearARB>3</RearARB>
///     ...
///   </Chassis>
///   <Dampers>
///     <FrontRebound>8</FrontRebound>
///     ...
///   </Dampers>
///   <Tires>
///     <LeftFrontPressure>138</LeftFrontPressure>
///     ...
///   </Tires>
///   <Aero>
///     <FrontWing>8</FrontWing>
///     <RearWing>10</RearWing>
///   </Aero>
/// </iRacingSetup>
/// 
/// Usage:
/// var setup = await parser.ParseSetupFileAsync("mysetup.sto");
/// Console.WriteLine($"Front wing: {setup.Aero.FrontWing}, Rear wing: {setup.Aero.RearWing}");
/// 
/// var diff = parser.CompareSetups(baseline, modified);
/// Console.WriteLine($"{diff.Changes.Count} parameters changed");
/// </summary>
public class SetupFileParser
{
    private readonly HtmlSetupParser _htmlParser = new();
    
    /// <summary>
    /// Parse iRacing setup file (.sto binary or .htm HTML export)
    /// RECOMMENDED: Use .htm files exported from iRacing garage for better compatibility
    /// </summary>
    public async Task<SetupData?> ParseSetupFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Setup file not found: {filePath}");
        }
        
        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            // HTML files (.htm/.html) - RECOMMENDED FORMAT
            if (extension == ".htm" || extension == ".html")
            {
                var setupConfig = _htmlParser.ParseHtmlFile(filePath);
                return ConvertToSetupData(setupConfig);
            }
            
            // Binary .sto files
            // Check if file is binary (first byte is 0x03 for .sto files)
            var firstBytes = new byte[4];
            using (var fs = File.OpenRead(filePath))
            {
                await fs.ReadAsync(firstBytes, 0, 4);
            }
            
            // If binary format (0x03 00 00 00), throw informative error
            if (firstBytes[0] == 0x03 && firstBytes[1] == 0x00)
            {
                throw new NotSupportedException(
                    "iRacing .sto files are in binary format. " +
                    "Binary parsing is not yet implemented. " +
                    "💡 TIP: Export your setup as HTML from iRacing garage (File → Export as HTML) " +
                    "for better compatibility and human readability!");
            }
            
            // Try XML parsing (for future XML export support)
            var xmlContent = await File.ReadAllTextAsync(filePath);
            return ParseSetupXml(xmlContent);
        }
        catch (NotSupportedException)
        {
            throw; // Re-throw binary format error
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse setup file: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Convert HTML-parsed SetupConfiguration to SetupData format
    /// </summary>
    private SetupData? ConvertToSetupData(SetupConfiguration config)
    {
        try
        {
            if (string.IsNullOrEmpty(config.SetupData)) return null;
            
            var parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.SetupData);
            if (parameters == null) return null;
            
            var setup = new SetupData();
            
            // Helper to safely get numeric values
            float GetFloat(string key) => parameters.ContainsKey(key) && parameters[key].ValueKind == JsonValueKind.Number 
                ? parameters[key].GetSingle() 
                : 0f;
            
            // Aero
            setup.Aero.FrontWing = GetFloat("Aero_FrontWing");
            setup.Aero.RearWing = GetFloat("Aero_RearWing");
            
            // Chassis/ARB - explicit nullable conversion
            var frontARB = GetFloat("Front_ARB");
            var rearARB = GetFloat("Rear_ARB");
            setup.Chassis.FrontARB = frontARB != 0 ? (float?)frontARB : null;
            setup.Chassis.RearARB = rearARB != 0 ? (float?)rearARB : null;
            setup.Chassis.BrakeBias = GetFloat("Front_BrakeBias");
            
            // Tires (pressures)
            setup.Tires.LeftFrontPressure = GetFloat("LEFTFRONT_ColdPressure");
            setup.Tires.RightFrontPressure = GetFloat("RIGHTFRONT_ColdPressure");
            setup.Tires.LeftRearPressure = GetFloat("LEFTREAR_ColdPressure");
            setup.Tires.RightRearPressure = GetFloat("RIGHTREAR_ColdPressure");
            
            // Ride heights
            setup.Chassis.FrontRideHeight = GetFloat("LF_RideHeight");
            setup.Chassis.RearRideHeight = GetFloat("Rear_RideHeight");
            
            // Springs
            setup.Chassis.FrontSpring = GetFloat("LF_SpringRate");
            setup.Chassis.RearSpring = GetFloat("LR_SpringRate");
            
            // Camber/Toe (average left/right)
            var lfCamber = GetFloat("LF_Camber");
            var rfCamber = GetFloat("RF_Camber");
            setup.Tires.LeftFrontCamber = lfCamber;
            setup.Tires.RightFrontCamber = rfCamber;
            
            var lrCamber = GetFloat("LR_Camber");
            var rrCamber = GetFloat("RR_Camber");
            setup.Tires.LeftRearCamber = lrCamber;
            setup.Tires.RightRearCamber = rrCamber;
            
            return setup;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SetupFileParser] ⚠️ Error converting HTML setup: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Parse setup XML content
    /// </summary>
    private SetupData ParseSetupXml(string xmlContent)
    {
        var setup = new SetupData();
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        
        // Root element should be <iRacingSetup>
        var root = doc.DocumentElement;
        if (root == null || root.Name != "iRacingSetup")
        {
            throw new InvalidOperationException("Invalid iRacing setup file (missing iRacingSetup root)");
        }
        
        // Parse sections
        ParseChassisSection(root, setup);
        ParseDampersSection(root, setup);
        ParseTiresSection(root, setup);
        ParseAeroSection(root, setup);
        ParseBrakesSection(root, setup);
        
        return setup;
    }
    
    /// <summary>
    /// Parse Chassis section (ARB, ride height, springs)
    /// </summary>
    private void ParseChassisSection(XmlElement root, SetupData setup)
    {
        var chassis = root.SelectSingleNode("Chassis") as XmlElement;
        if (chassis == null) return;
        
        setup.Chassis.FrontARB = ParseFloat(chassis, "FrontARB");
        setup.Chassis.RearARB = ParseFloat(chassis, "RearARB");
        setup.Chassis.FrontRideHeight = ParseFloat(chassis, "FrontRideHeight");
        setup.Chassis.RearRideHeight = ParseFloat(chassis, "RearRideHeight");
        setup.Chassis.FrontSpring = ParseFloat(chassis, "FrontSpring");
        setup.Chassis.RearSpring = ParseFloat(chassis, "RearSpring");
        setup.Chassis.FrontWeight = ParseFloat(chassis, "FrontWeight");
        setup.Chassis.RearWeight = ParseFloat(chassis, "RearWeight");
        setup.Chassis.BrakeBias = ParseFloat(chassis, "BrakeBias");
    }
    
    /// <summary>
    /// Parse Dampers section (compression, rebound)
    /// </summary>
    private void ParseDampersSection(XmlElement root, SetupData setup)
    {
        var dampers = root.SelectSingleNode("Dampers") as XmlElement;
        if (dampers == null) return;
        
        setup.Dampers.FrontCompression = ParseFloat(dampers, "FrontCompression");
        setup.Dampers.RearCompression = ParseFloat(dampers, "RearCompression");
        setup.Dampers.FrontRebound = ParseFloat(dampers, "FrontRebound");
        setup.Dampers.RearRebound = ParseFloat(dampers, "RearRebound");
        setup.Dampers.FrontBumpStiffness = ParseFloat(dampers, "FrontBumpStiffness");
        setup.Dampers.RearBumpStiffness = ParseFloat(dampers, "RearBumpStiffness");
    }
    
    /// <summary>
    /// Parse Tires section (pressure, camber, toe)
    /// </summary>
    private void ParseTiresSection(XmlElement root, SetupData setup)
    {
        var tires = root.SelectSingleNode("Tires") as XmlElement;
        if (tires == null) return;
        
        // Tire pressures (kPa)
        setup.Tires.LeftFrontPressure = ParseFloat(tires, "LeftFrontPressure");
        setup.Tires.RightFrontPressure = ParseFloat(tires, "RightFrontPressure");
        setup.Tires.LeftRearPressure = ParseFloat(tires, "LeftRearPressure");
        setup.Tires.RightRearPressure = ParseFloat(tires, "RightRearPressure");
        
        // Camber (degrees)
        setup.Tires.LeftFrontCamber = ParseFloat(tires, "LeftFrontCamber");
        setup.Tires.RightFrontCamber = ParseFloat(tires, "RightFrontCamber");
        setup.Tires.LeftRearCamber = ParseFloat(tires, "LeftRearCamber");
        setup.Tires.RightRearCamber = ParseFloat(tires, "RightRearCamber");
        
        // Toe (degrees)
        setup.Tires.LeftFrontToe = ParseFloat(tires, "LeftFrontToe");
        setup.Tires.RightFrontToe = ParseFloat(tires, "RightFrontToe");
        setup.Tires.LeftRearToe = ParseFloat(tires, "LeftRearToe");
        setup.Tires.RightRearToe = ParseFloat(tires, "RightRearToe");
    }
    
    /// <summary>
    /// Parse Aero section (wings, ride height)
    /// </summary>
    private void ParseAeroSection(XmlElement root, SetupData setup)
    {
        var aero = root.SelectSingleNode("Aero") as XmlElement;
        if (aero == null) return;
        
        setup.Aero.FrontWing = ParseInt(aero, "FrontWing");
        setup.Aero.RearWing = ParseInt(aero, "RearWing");
        setup.Aero.RakeAngle = ParseFloat(aero, "RakeAngle");
    }
    
    /// <summary>
    /// Parse Brakes section (bias, pressure)
    /// </summary>
    private void ParseBrakesSection(XmlElement root, SetupData setup)
    {
        var brakes = root.SelectSingleNode("Brakes") as XmlElement;
        if (brakes == null) return;
        
        setup.Brakes.BrakeBias = ParseFloat(brakes, "BrakeBias");
        setup.Brakes.BrakePressure = ParseFloat(brakes, "BrakePressure");
    }
    
    /// <summary>
    /// Compare two setups and return diff
    /// </summary>
    public SetupDiff CompareSetups(SetupData baseline, SetupData modified)
    {
        var diff = new SetupDiff
        {
            BaselineSetup = baseline,
            ModifiedSetup = modified,
            Changes = new List<SetupChange>()
        };
        
        // Compare Chassis
        CompareParameter(diff, "Chassis", "FrontARB", baseline.Chassis.FrontARB, modified.Chassis.FrontARB, "");
        CompareParameter(diff, "Chassis", "RearARB", baseline.Chassis.RearARB, modified.Chassis.RearARB, "");
        CompareParameter(diff, "Chassis", "FrontRideHeight", baseline.Chassis.FrontRideHeight, modified.Chassis.FrontRideHeight, "mm");
        CompareParameter(diff, "Chassis", "RearRideHeight", baseline.Chassis.RearRideHeight, modified.Chassis.RearRideHeight, "mm");
        CompareParameter(diff, "Chassis", "FrontSpring", baseline.Chassis.FrontSpring, modified.Chassis.FrontSpring, "N/mm");
        CompareParameter(diff, "Chassis", "RearSpring", baseline.Chassis.RearSpring, modified.Chassis.RearSpring, "N/mm");
        CompareParameter(diff, "Chassis", "BrakeBias", baseline.Chassis.BrakeBias, modified.Chassis.BrakeBias, "%");
        
        // Compare Dampers
        CompareParameter(diff, "Dampers", "FrontCompression", baseline.Dampers.FrontCompression, modified.Dampers.FrontCompression, "");
        CompareParameter(diff, "Dampers", "RearCompression", baseline.Dampers.RearCompression, modified.Dampers.RearCompression, "");
        CompareParameter(diff, "Dampers", "FrontRebound", baseline.Dampers.FrontRebound, modified.Dampers.FrontRebound, "");
        CompareParameter(diff, "Dampers", "RearRebound", baseline.Dampers.RearRebound, modified.Dampers.RearRebound, "");
        
        // Compare Tires (pressures only, skip camber/toe unless significant)
        CompareParameter(diff, "Tires", "LeftFrontPressure", baseline.Tires.LeftFrontPressure, modified.Tires.LeftFrontPressure, "kPa");
        CompareParameter(diff, "Tires", "RightFrontPressure", baseline.Tires.RightFrontPressure, modified.Tires.RightFrontPressure, "kPa");
        CompareParameter(diff, "Tires", "LeftRearPressure", baseline.Tires.LeftRearPressure, modified.Tires.LeftRearPressure, "kPa");
        CompareParameter(diff, "Tires", "RightRearPressure", baseline.Tires.RightRearPressure, modified.Tires.RightRearPressure, "kPa");
        
        // Compare Aero
        CompareParameter(diff, "Aero", "FrontWing", baseline.Aero.FrontWing, modified.Aero.FrontWing, "");
        CompareParameter(diff, "Aero", "RearWing", baseline.Aero.RearWing, modified.Aero.RearWing, "");
        
        return diff;
    }
    
    /// <summary>
    /// Compare single parameter (int)
    /// </summary>
    private void CompareParameter(SetupDiff diff, string category, string name, int? baseline, int? modified, string unit)
    {
        if (!baseline.HasValue || !modified.HasValue) return;
        if (baseline.Value == modified.Value) return;
        
        diff.Changes.Add(new SetupChange
        {
            Category = category,
            Parameter = name,
            BaselineValue = baseline.Value.ToString(),
            ModifiedValue = modified.Value.ToString(),
            Delta = (modified.Value - baseline.Value).ToString(),
            Unit = unit
        });
    }
    
    /// <summary>
    /// Compare single parameter (float)
    /// </summary>
    private void CompareParameter(SetupDiff diff, string category, string name, float? baseline, float? modified, string unit)
    {
        if (!baseline.HasValue || !modified.HasValue) return;
        if (Math.Abs(baseline.Value - modified.Value) < 0.001f) return;  // Ignore tiny differences
        
        diff.Changes.Add(new SetupChange
        {
            Category = category,
            Parameter = name,
            BaselineValue = baseline.Value.ToString("F2"),
            ModifiedValue = modified.Value.ToString("F2"),
            Delta = (modified.Value - baseline.Value).ToString("+0.00;-0.00"),
            Unit = unit
        });
    }
    
    /// <summary>
    /// Parse integer XML node
    /// </summary>
    private int? ParseInt(XmlElement parent, string nodeName)
    {
        var node = parent.SelectSingleNode(nodeName);
        if (node == null || string.IsNullOrEmpty(node.InnerText)) return null;
        
        if (int.TryParse(node.InnerText, out int value))
            return value;
        
        return null;
    }
    
    /// <summary>
    /// Parse float XML node
    /// </summary>
    private float? ParseFloat(XmlElement parent, string nodeName)
    {
        var node = parent.SelectSingleNode(nodeName);
        if (node == null || string.IsNullOrEmpty(node.InnerText)) return null;
        
        if (float.TryParse(node.InnerText, out float value))
            return value;
        
        return null;
    }
    
    /// <summary>
    /// Serialize setup to JSON for storage
    /// </summary>
    public string SerializeSetup(SetupData setup)
    {
        return JsonSerializer.Serialize(setup, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
    
    /// <summary>
    /// Deserialize setup from JSON
    /// </summary>
    public SetupData? DeserializeSetup(string json)
    {
        return JsonSerializer.Deserialize<SetupData>(json);
    }
}

// ===== DATA MODELS =====

/// <summary>
/// Complete setup data structure
/// </summary>
public class SetupData
{
    public ChassisSetup Chassis { get; set; } = new();
    public DampersSetup Dampers { get; set; } = new();
    public TiresSetup Tires { get; set; } = new();
    public AeroSetup Aero { get; set; } = new();
    public BrakesSetup Brakes { get; set; } = new();
}

public class ChassisSetup
{
    public float? FrontARB { get; set; }        // Anti-roll bar (1-11 clicks, may have fractional values)
    public float? RearARB { get; set; }
    public float? FrontRideHeight { get; set; } // mm
    public float? RearRideHeight { get; set; }
    public float? FrontSpring { get; set; }     // N/mm
    public float? RearSpring { get; set; }
    public float? FrontWeight { get; set; }     // kg
    public float? RearWeight { get; set; }
    public float? BrakeBias { get; set; }       // % front
}

public class DampersSetup
{
    public float? FrontCompression { get; set; }   // Clicks
    public float? RearCompression { get; set; }
    public float? FrontRebound { get; set; }
    public float? RearRebound { get; set; }
    public float? FrontBumpStiffness { get; set; }
    public float? RearBumpStiffness { get; set; }
}

public class TiresSetup
{
    // Pressures (kPa)
    public float? LeftFrontPressure { get; set; }
    public float? RightFrontPressure { get; set; }
    public float? LeftRearPressure { get; set; }
    public float? RightRearPressure { get; set; }
    
    // Camber (degrees)
    public float? LeftFrontCamber { get; set; }
    public float? RightFrontCamber { get; set; }
    public float? LeftRearCamber { get; set; }
    public float? RightRearCamber { get; set; }
    
    // Toe (degrees)
    public float? LeftFrontToe { get; set; }
    public float? RightFrontToe { get; set; }
    public float? LeftRearToe { get; set; }
    public float? RightRearToe { get; set; }
}

public class AeroSetup
{
    public float? FrontWing { get; set; }   // Clicks or degrees (may have fractional values)
    public float? RearWing { get; set; }
    public float? RakeAngle { get; set; }   // degrees
}

public class BrakesSetup
{
    public float? BrakeBias { get; set; }       // % front
    public float? BrakePressure { get; set; }   // %
}

/// <summary>
/// Setup comparison result (diff between baseline and modified)
/// </summary>
public class SetupDiff
{
    public SetupData BaselineSetup { get; set; } = new();
    public SetupData ModifiedSetup { get; set; } = new();
    public List<SetupChange> Changes { get; set; } = new();
    
    public int ChangeCount => Changes.Count;
    
    public string GetSummary()
    {
        if (Changes.Count == 0)
            return "No changes detected";
        
        var summary = $"{Changes.Count} parameter(s) changed:\n";
        foreach (var change in Changes)
        {
            summary += $"  • {change.Category}.{change.Parameter}: {change.BaselineValue} → {change.ModifiedValue} ({change.Delta} {change.Unit})\n";
        }
        
        return summary.TrimEnd();
    }
}

public class SetupChange
{
    public string Category { get; set; } = "";      // "Chassis", "Dampers", "Tires", "Aero"
    public string Parameter { get; set; } = "";     // "FrontARB", "RearWing", etc.
    public string BaselineValue { get; set; } = ""; // "5"
    public string ModifiedValue { get; set; } = ""; // "7"
    public string Delta { get; set; } = "";         // "+2"
    public string Unit { get; set; } = "";          // "kPa", "mm", "%", ""
}
