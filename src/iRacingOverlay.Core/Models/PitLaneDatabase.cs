namespace iRacingOverlay.Core.Models;

/// <summary>
/// Track-specific pit lane length database
/// Based on iRacing track measurements and community data
/// Used for more accurate pit stop time estimates
/// </summary>
public static class PitLaneDatabase
{
    /// <summary>
    /// Default pit lane length as percentage of track length (conservative estimate)
    /// </summary>
    public const float DEFAULT_PIT_LANE_PERCENTAGE = 0.175f; // 17.5% of track length

    /// <summary>
    /// Get pit lane length percentage for a specific track
    /// </summary>
    /// <param name="trackName">iRacing track name</param>
    /// <returns>Pit lane length as percentage of full track length (0-1 range)</returns>
    /// <remarks>
    /// Pit lane percentage = PitLaneLength / TrackLength
    /// This is multiplied by track length to get actual pit lane meters
    /// </remarks>
    public static float GetPitLanePercentage(string trackName)
    {
        // Normalize track name (lowercase, remove spaces/special chars for matching)
        string normalized = trackName.ToLowerInvariant().Replace(" ", "").Replace("-", "");

        // Known track-specific pit lane percentages
        var pitLaneMap = new Dictionary<string, float>
        {
            // ===== ROAD COURSES (Varied pit lane lengths) =====

            // Long pit lanes (>20% of track)
            { "spa", 0.30f },                    // Spa-Francorchamps - very long pit lane (~2.1km / 7km = 30%)
            { "lemans", 0.28f },                 // Circuit de la Sarthe - famous long pit lane
            { "nurburgringnordschleife", 0.25f }, // Nürburgring Nordschleife - long due to track length
            { "nurburgringcombined", 0.23f },     // Nürburgring Combined (GP + Nordschleife)
            { "sebring", 0.22f },                 // Sebring - long pit lane

            // Medium pit lanes (15-20%)
            { "interlagos", 0.18f },             // Interlagos
            { "silverstone", 0.17f },            // Silverstone
            { "monza", 0.17f },                  // Monza
            { "watkinsglen", 0.16f },            // Watkins Glen
            { "roadatlanta", 0.16f },            // Road Atlanta
            { "barber", 0.15f },                 // Barber Motorsports Park
            { "midohio", 0.15f },                // Mid-Ohio
            { "roadamerica", 0.15f },            // Road America

            // Short pit lanes (10-15%)
            { "laguna", 0.14f },                  // Laguna Seca
            { "lagunaseca", 0.14f },              // Laguna Seca (alternate name)
            { "limerock", 0.13f },                // Lime Rock Park
            { "oulton", 0.12f },                  // Oulton Park
            { "snetterton", 0.12f },              // Snetterton
            { "summitpoint", 0.11f },             // Summit Point
            { "okayama", 0.10f },                 // Okayama International Circuit

            // ===== NASCAR OVALS (Short pit lanes) =====
            // Ovals typically have short pit lanes (10-12% of track)

            { "charlotte", 0.11f },               // Charlotte Motor Speedway
            { "daytona", 0.10f },                 // Daytona International Speedway
            { "talladega", 0.10f },               // Talladega Superspeedway
            { "texas", 0.11f },                   // Texas Motor Speedway
            { "lasvegas", 0.11f },                // Las Vegas Motor Speedway
            { "atlanta", 0.10f },                 // Atlanta Motor Speedway
            { "homestead", 0.11f },               // Homestead-Miami Speedway
            { "kansas", 0.11f },                  // Kansas Speedway
            { "kentucky", 0.11f },                // Kentucky Speedway
            { "michigan", 0.10f },                // Michigan International Speedway
            { "phoenix", 0.12f },                 // Phoenix Raceway
            { "pocono", 0.13f },                  // Pocono Raceway (triangular, unique layout)

            // Short tracks (10-12%)
            { "bristol", 0.12f },                 // Bristol Motor Speedway
            { "martinsville", 0.11f },            // Martinsville Speedway
            { "richmond", 0.12f },                // Richmond Raceway
            { "nashville", 0.11f },               // Nashville Superspeedway

            // ===== STREET CIRCUITS (Variable, often short) =====
            { "longbeach", 0.14f },               // Long Beach
            { "belle", 0.13f },                   // Belle Isle
            { "detroit", 0.13f },                 // Detroit (alternate name)
            { "toronto", 0.13f },                 // Toronto street circuit
            { "stpetersburg", 0.14f },            // St. Petersburg

            // ===== DIRT TRACKS (Short pit lanes) =====
            { "eldora", 0.11f },                  // Eldora Speedway
            { "knoxville", 0.10f },               // Knoxville Raceway
            { "williamsgrove", 0.10f },           // Williams Grove Speedway
            { "volusia", 0.11f },                 // Volusia Speedway Park
            { "lanier", 0.10f },                  // Lanier National Speedway

            // ===== SPECIAL TRACKS =====
            { "northwilkesboro", 0.11f },        // North Wilkesboro Speedway
            { "irwindale", 0.10f },              // Irwindale Speedway
            { "southboston", 0.11f },            // South Boston Speedway
        };

        // Try to get track-specific percentage
        if (pitLaneMap.TryGetValue(normalized, out float percentage))
        {
            return percentage;
        }

        // Fallback: Estimate based on track name patterns
        // Ovals typically have shorter pit lanes
        if (normalized.Contains("speedway") || normalized.Contains("motor") ||
            normalized.Contains("super") || normalized.Contains("daytona") ||
            normalized.Contains("talladega"))
        {
            return 0.11f; // Conservative oval estimate
        }

        // Street circuits tend to have shorter pit lanes
        if (normalized.Contains("street") || normalized.Contains("city") ||
            normalized.Contains("monaco") || normalized.Contains("singapore"))
        {
            return 0.13f; // Street circuit estimate
        }

        // Road courses - use default
        return DEFAULT_PIT_LANE_PERCENTAGE;
    }

    /// <summary>
    /// Calculate estimated pit lane length in meters
    /// </summary>
    /// <param name="trackName">iRacing track name</param>
    /// <param name="trackLength">Full track length in meters</param>
    /// <returns>Estimated pit lane length in meters</returns>
    public static float CalculatePitLaneLength(string trackName, float trackLength)
    {
        float percentage = GetPitLanePercentage(trackName);
        return trackLength * percentage;
    }

    /// <summary>
    /// Calculate pit transit time (time spent driving through pit lane)
    /// </summary>
    /// <param name="trackName">iRacing track name</param>
    /// <param name="trackLength">Full track length in meters</param>
    /// <param name="pitSpeedLimit">Pit speed limit in m/s</param>
    /// <returns>Pit transit time in seconds</returns>
    public static float CalculatePitTransitTime(string trackName, float trackLength, float pitSpeedLimit)
    {
        if (pitSpeedLimit <= 0)
            pitSpeedLimit = 16.7f; // Default 60 km/h = 16.7 m/s

        float pitLaneLength = CalculatePitLaneLength(trackName, trackLength);
        return pitLaneLength / pitSpeedLimit;
    }

    /// <summary>
    /// Get track category description for pit lane characteristics
    /// </summary>
    public static string GetTrackCategory(string trackName)
    {
        string normalized = trackName.ToLowerInvariant().Replace(" ", "").Replace("-", "");

        if (normalized.Contains("speedway") || normalized.Contains("motor"))
            return "Oval (short pit lane ~11%)";
        else if (normalized.Contains("street") || normalized.Contains("city"))
            return "Street Circuit (short pit lane ~13%)";
        else if (normalized.Contains("spa") || normalized.Contains("lemans") || normalized.Contains("nurburgring"))
            return "Endurance Track (long pit lane >20%)";
        else
            return "Road Course (standard pit lane ~17%)";
    }
}
