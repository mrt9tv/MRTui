namespace iRacingOverlay.Core.Models;

/// <summary>
/// Sector timing information parsed from SessionInfo YAML
/// Track sectors are defined once at session start and used for lap-time splitting
/// </summary>
public class SectorInfo
{
    /// <summary>
    /// Sector number (0-indexed)
    /// Typical tracks have 3 sectors: 0, 1, 2
    /// </summary>
    public int SectorNum { get; set; }
    
    /// <summary>
    /// Sector start position as percentage around lap (0.0 - 1.0)
    /// Example: Spa-Francorchamps (3 sectors)
    ///   Sector 0: 0.000000 (Start/Finish)
    ///   Sector 1: 0.333333 (~33% through lap)
    ///   Sector 2: 0.666667 (~66% through lap)
    /// </summary>
    public float SectorStartPct { get; set; }
    
    /// <summary>
    /// Create sector info from YAML data
    /// </summary>
    public SectorInfo(int sectorNum, float sectorStartPct)
    {
        SectorNum = sectorNum;
        SectorStartPct = sectorStartPct;
    }
}

/// <summary>
/// Collection of track sectors for timing calculations
/// </summary>
public class TrackSectors
{
    /// <summary>
    /// All sectors for current track
    /// </summary>
    public List<SectorInfo> Sectors { get; set; } = new();
    
    /// <summary>
    /// Number of sectors on this track
    /// </summary>
    public int SectorCount => Sectors.Count;
    
    /// <summary>
    /// Get sector number for given track position
    /// </summary>
    /// <param name="lapDistPct">Track position (0.0 - 1.0)</param>
    /// <returns>Current sector number (0-indexed)</returns>
    public int GetCurrentSector(float lapDistPct)
    {
        if (Sectors.Count == 0)
            return 0;
        
        // Find sector by comparing position to sector start boundaries
        for (int i = Sectors.Count - 1; i >= 0; i--)
        {
            if (lapDistPct >= Sectors[i].SectorStartPct)
            {
                return Sectors[i].SectorNum;
            }
        }
        
        return 0;  // Fallback to first sector
    }
    
    /// <summary>
    /// Get sector start position
    /// </summary>
    public float GetSectorStartPct(int sectorNum)
    {
        var sector = Sectors.FirstOrDefault(s => s.SectorNum == sectorNum);
        return sector?.SectorStartPct ?? 0.0f;
    }
}

/// <summary>
/// Parser for SessionInfo YAML - SplitTimeInfo section
/// Extracts sector definitions for lap timing
/// </summary>
public static class SectorParser
{
    /// <summary>
    /// Parse sectors from SessionInfo YAML string
    /// Looks for SplitTimeInfo section with Sectors array
    /// </summary>
    /// <param name="sessionInfoYaml">Raw YAML string from iRacing SDK</param>
    /// <returns>Parsed track sectors</returns>
    /// <example>
    /// Example YAML:
    /// <code>
    /// SplitTimeInfo:
    ///   Sectors:
    ///    - SectorNum: 0
    ///      SectorStartPct: 0.000000
    ///    - SectorNum: 1
    ///      SectorStartPct: 0.333333
    ///    - SectorNum: 2
    ///      SectorStartPct: 0.666667
    /// </code>
    /// </example>
    public static TrackSectors ParseFromYaml(string sessionInfoYaml)
    {
        var trackSectors = new TrackSectors();
        
        if (string.IsNullOrEmpty(sessionInfoYaml))
            return trackSectors;
        
        try
        {
            // Find SplitTimeInfo section
            int splitTimeInfoIndex = sessionInfoYaml.IndexOf("SplitTimeInfo:", StringComparison.Ordinal);
            if (splitTimeInfoIndex == -1)
                return trackSectors;
            
            // Find Sectors array within SplitTimeInfo
            int sectorsIndex = sessionInfoYaml.IndexOf("Sectors:", splitTimeInfoIndex, StringComparison.Ordinal);
            if (sectorsIndex == -1)
                return trackSectors;
            
            // Parse each sector entry
            var lines = sessionInfoYaml.Substring(sectorsIndex).Split('\n');
            SectorInfo? currentSector = null;
            
            foreach (var line in lines)
            {
                // Stop at next top-level section
                if (line.Length > 0 && !line.StartsWith(" ") && !line.StartsWith("\t"))
                    break;
                
                // New sector entry (starts with " - ")
                if (line.TrimStart().StartsWith("- SectorNum:"))
                {
                    if (currentSector != null)
                    {
                        trackSectors.Sectors.Add(currentSector);
                    }
                    
                    var numStr = line.Split(':')[1].Trim();
                    if (int.TryParse(numStr, out int sectorNum))
                    {
                        currentSector = new SectorInfo(sectorNum, 0.0f);
                    }
                }
                // Sector start percentage
                else if (line.Contains("SectorStartPct:") && currentSector != null)
                {
                    var pctStr = line.Split(':')[1].Trim();
                    if (float.TryParse(pctStr, System.Globalization.NumberStyles.Float, 
                        System.Globalization.CultureInfo.InvariantCulture, out float startPct))
                    {
                        currentSector.SectorStartPct = startPct;
                    }
                }
            }
            
            // Add last sector
            if (currentSector != null)
            {
                trackSectors.Sectors.Add(currentSector);
            }
        }
        catch (Exception ex)
        {
            // Log error but return empty sectors (graceful degradation)
            Console.WriteLine($"Error parsing sectors from YAML: {ex.Message}");
        }
        
        return trackSectors;
    }
}
