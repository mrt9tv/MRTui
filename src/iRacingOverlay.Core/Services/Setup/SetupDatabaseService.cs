using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace iRacingOverlay.Core.Services.Setup;

/// <summary>
/// SQLite database service for Setup Engineering Mode
/// Manages setup sessions, configurations, and lap telemetry
/// 
/// Database Location: %APPDATA%/MRTOverlay/MRTOverlay.db
/// Schema: SetupSessions, SetupConfigurations, LapTelemetry tables
/// 
/// Features:
/// - ACID compliance (no data corruption from crashes)
/// - Fast queries (< 10ms with indexes)
/// - Zero-configuration embedded database
/// - Portable single-file storage
/// </summary>
public class SetupDatabaseService : IDisposable
{
    private readonly string _databasePath;
    private SqliteConnection? _connection;
    private readonly object _dbLock = new object();
    
    public SetupDatabaseService(string? databaseDirectory = null)
    {
        // Default to %APPDATA%/MRTOverlay/
        databaseDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MRTOverlay");
        
        Directory.CreateDirectory(databaseDirectory);
        _databasePath = Path.Combine(databaseDirectory, "MRTOverlay.db");
        
        Console.WriteLine($"[SetupDatabase] Database path: {_databasePath}");
    }
    
    /// <summary>
    /// Initialize database connection and create schema if needed
    /// </summary>
    public async Task InitializeAsync()
    {
        lock (_dbLock)
        {
            _connection = new SqliteConnection($"Data Source={_databasePath}");
            _connection.Open();
        }
        
        await CreateSchemaAsync();
        Console.WriteLine("[SetupDatabase] ✅ Database initialized");
    }
    
    /// <summary>
    /// Create database schema (Setup Engineering tables)
    /// </summary>
    private async Task CreateSchemaAsync()
    {
        var schema = @"
            -- Setup Engineering: Sessions
            CREATE TABLE IF NOT EXISTS SetupSessions (
                SessionId TEXT PRIMARY KEY,
                TrackName TEXT NOT NULL,
                CarName TEXT NOT NULL,
                SessionType TEXT,        -- 'Practice', 'TestDay', 'Qualifying'
                StartTime DATETIME NOT NULL,
                EndTime DATETIME,
                Weather TEXT,            -- JSON: {""AirTemp"": 25, ""TrackTemp"": 35}
                Notes TEXT,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE INDEX IF NOT EXISTS idx_sessions_track_car 
                ON SetupSessions(TrackName, CarName);
            CREATE INDEX IF NOT EXISTS idx_sessions_start_time 
                ON SetupSessions(StartTime DESC);
            
            -- Setup Engineering: Configurations
            CREATE TABLE IF NOT EXISTS SetupConfigurations (
                SetupId TEXT PRIMARY KEY,
                SessionId TEXT NOT NULL,
                SetupName TEXT NOT NULL,
                SetupData TEXT,          -- JSON: iRacing setup file content
                SetupChanges TEXT,       -- JSON: Changes from baseline
                Timestamp DATETIME NOT NULL,
                IsBaseline INTEGER DEFAULT 0,  -- 1 if this is the baseline setup
                LapCount INTEGER DEFAULT 0,
                BestLapTime REAL,
                AvgLapTime REAL,
                FOREIGN KEY (SessionId) REFERENCES SetupSessions(SessionId) ON DELETE CASCADE
            );
            
            CREATE INDEX IF NOT EXISTS idx_setups_session 
                ON SetupConfigurations(SessionId, Timestamp);
            CREATE INDEX IF NOT EXISTS idx_setups_name 
                ON SetupConfigurations(SetupName);
            
            -- Setup Engineering: Lap Telemetry
            CREATE TABLE IF NOT EXISTS LapTelemetry (
                LapId TEXT PRIMARY KEY,
                SetupId TEXT NOT NULL,
                LapNumber INTEGER NOT NULL,
                LapTime REAL,
                Sector1 REAL,
                Sector2 REAL,
                Sector3 REAL,
                FuelUsed REAL,
                AvgTireTemp_FL REAL,
                AvgTireTemp_FR REAL,
                AvgTireTemp_RL REAL,
                AvgTireTemp_RR REAL,
                AvgSpeed REAL,
                MaxSpeed REAL,
                AvgThrottle REAL,
                IsValid INTEGER DEFAULT 1,    -- 0 if outlier/invalid
                IncidentCount INTEGER DEFAULT 0,
                TelemetryData BLOB,           -- Compressed full telemetry (optional)
                Timestamp DATETIME NOT NULL,
                FOREIGN KEY (SetupId) REFERENCES SetupConfigurations(SetupId) ON DELETE CASCADE
            );
            
            CREATE INDEX IF NOT EXISTS idx_laps_setup 
                ON LapTelemetry(SetupId, LapNumber);
            CREATE INDEX IF NOT EXISTS idx_laps_valid 
                ON LapTelemetry(SetupId, IsValid);
        ";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = schema;
        await command.ExecuteNonQueryAsync();
    }
    
    // ===== SESSION CRUD OPERATIONS =====
    
    /// <summary>
    /// Create new setup session
    /// </summary>
    public async Task<string> CreateSessionAsync(
        string trackName,
        string carName,
        string sessionType,
        string? notes = null)
    {
        var sessionId = Guid.NewGuid().ToString();
        var weather = new { AirTemp = 0, TrackTemp = 0 }; // Will be updated from telemetry
        
        var sql = @"
            INSERT INTO SetupSessions (SessionId, TrackName, CarName, SessionType, StartTime, Weather, Notes)
            VALUES (@SessionId, @TrackName, @CarName, @SessionType, @StartTime, @Weather, @Notes)
        ";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@SessionId", sessionId);
        command.Parameters.AddWithValue("@TrackName", trackName);
        command.Parameters.AddWithValue("@CarName", carName);
        command.Parameters.AddWithValue("@SessionType", sessionType);
        command.Parameters.AddWithValue("@StartTime", DateTime.UtcNow);
        command.Parameters.AddWithValue("@Weather", JsonSerializer.Serialize(weather));
        command.Parameters.AddWithValue("@Notes", notes ?? (object)DBNull.Value);
        
        await command.ExecuteNonQueryAsync();
        
        Console.WriteLine($"[SetupDatabase] ✅ Created session: {sessionId} ({trackName}, {carName})");
        return sessionId;
    }
    
    /// <summary>
    /// Get session by ID
    /// </summary>
    public async Task<SetupSession?> GetSessionAsync(string sessionId)
    {
        var sql = "SELECT * FROM SetupSessions WHERE SessionId = @SessionId";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@SessionId", sessionId);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SetupSession
            {
                SessionId = reader.GetString(0),
                TrackName = reader.GetString(1),
                CarName = reader.GetString(2),
                SessionType = reader.IsDBNull(3) ? null : reader.GetString(3),
                StartTime = reader.GetDateTime(4),
                EndTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                Weather = reader.IsDBNull(6) ? null : reader.GetString(6),
                Notes = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// List all sessions (most recent first)
    /// </summary>
    public async Task<List<SetupSession>> ListSessionsAsync(
        string? trackName = null,
        string? carName = null,
        int limit = 50)
    {
        var sql = @"
            SELECT * FROM SetupSessions 
            WHERE (@TrackName IS NULL OR TrackName = @TrackName)
              AND (@CarName IS NULL OR CarName = @CarName)
            ORDER BY StartTime DESC
            LIMIT @Limit
        ";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@TrackName", trackName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CarName", carName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Limit", limit);
        
        var sessions = new List<SetupSession>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new SetupSession
            {
                SessionId = reader.GetString(0),
                TrackName = reader.GetString(1),
                CarName = reader.GetString(2),
                SessionType = reader.IsDBNull(3) ? null : reader.GetString(3),
                StartTime = reader.GetDateTime(4),
                EndTime = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                Weather = reader.IsDBNull(6) ? null : reader.GetString(6),
                Notes = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        
        Console.WriteLine($"[SetupDatabase] Listed {sessions.Count} sessions");
        return sessions;
    }
    
    /// <summary>
    /// End session (set EndTime)
    /// </summary>
    public async Task EndSessionAsync(string sessionId)
    {
        var sql = "UPDATE SetupSessions SET EndTime = @EndTime WHERE SessionId = @SessionId";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@EndTime", DateTime.UtcNow);
        command.Parameters.AddWithValue("@SessionId", sessionId);
        
        await command.ExecuteNonQueryAsync();
        Console.WriteLine($"[SetupDatabase] ✅ Ended session: {sessionId}");
    }
    
    // ===== SETUP CONFIGURATION CRUD =====
    
    /// <summary>
    /// Add setup configuration to session
    /// </summary>
    public async Task<string> AddSetupAsync(
        string sessionId,
        string setupName,
        string? setupData = null,
        string? setupChanges = null,
        bool isBaseline = false)
    {
        var setupId = Guid.NewGuid().ToString();
        
        var sql = @"
            INSERT INTO SetupConfigurations 
            (SetupId, SessionId, SetupName, SetupData, SetupChanges, Timestamp, IsBaseline)
            VALUES (@SetupId, @SessionId, @SetupName, @SetupData, @SetupChanges, @Timestamp, @IsBaseline)
        ";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@SetupId", setupId);
        command.Parameters.AddWithValue("@SessionId", sessionId);
        command.Parameters.AddWithValue("@SetupName", setupName);
        command.Parameters.AddWithValue("@SetupData", setupData ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@SetupChanges", setupChanges ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
        command.Parameters.AddWithValue("@IsBaseline", isBaseline ? 1 : 0);
        
        await command.ExecuteNonQueryAsync();
        
        Console.WriteLine($"[SetupDatabase] ✅ Added setup: {setupName} ({setupId})");
        return setupId;
    }
    
    /// <summary>
    /// Get setup configuration by ID
    /// </summary>
    public async Task<SetupConfiguration?> GetSetupAsync(string setupId)
    {
        var sql = "SELECT * FROM SetupConfigurations WHERE SetupId = @SetupId";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@SetupId", setupId);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SetupConfiguration
            {
                SetupId = reader.GetString(0),
                SessionId = reader.GetString(1),
                SetupName = reader.GetString(2),
                SetupData = reader.IsDBNull(3) ? null : reader.GetString(3),
                SetupChanges = reader.IsDBNull(4) ? null : reader.GetString(4),
                Timestamp = reader.GetDateTime(5),
                IsBaseline = reader.GetInt32(6) == 1,
                LapCount = reader.GetInt32(7),
                BestLapTime = reader.IsDBNull(8) ? null : reader.GetFloat(8),
                AvgLapTime = reader.IsDBNull(9) ? null : reader.GetFloat(9)
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// List all setups in a session
    /// </summary>
    public async Task<List<SetupConfiguration>> ListSetupsAsync(string sessionId)
    {
        var sql = @"
            SELECT * FROM SetupConfigurations 
            WHERE SessionId = @SessionId 
            ORDER BY Timestamp ASC
        ";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@SessionId", sessionId);
        
        var setups = new List<SetupConfiguration>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            setups.Add(new SetupConfiguration
            {
                SetupId = reader.GetString(0),
                SessionId = reader.GetString(1),
                SetupName = reader.GetString(2),
                SetupData = reader.IsDBNull(3) ? null : reader.GetString(3),
                SetupChanges = reader.IsDBNull(4) ? null : reader.GetString(4),
                Timestamp = reader.GetDateTime(5),
                IsBaseline = reader.GetInt32(6) == 1,
                LapCount = reader.GetInt32(7),
                BestLapTime = reader.IsDBNull(8) ? null : reader.GetFloat(8),
                AvgLapTime = reader.IsDBNull(9) ? null : reader.GetFloat(9)
            });
        }
        
        return setups;
    }
    
    /// <summary>
    /// Update setup statistics (lap count, best/avg lap time)
    /// </summary>
    public async Task UpdateSetupStatsAsync(
        string setupId,
        int lapCount,
        float? bestLapTime = null,
        float? avgLapTime = null)
    {
        var sql = @"
            UPDATE SetupConfigurations 
            SET LapCount = @LapCount,
                BestLapTime = COALESCE(@BestLapTime, BestLapTime),
                AvgLapTime = @AvgLapTime
            WHERE SetupId = @SetupId
        ";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@SetupId", setupId);
        command.Parameters.AddWithValue("@LapCount", lapCount);
        command.Parameters.AddWithValue("@BestLapTime", bestLapTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AvgLapTime", avgLapTime ?? (object)DBNull.Value);
        
        await command.ExecuteNonQueryAsync();
    }
    
    // ===== LAP TELEMETRY CRUD =====
    
    /// <summary>
    /// Add lap telemetry
    /// </summary>
    public async Task<string> AddLapAsync(
        string setupId,
        int lapNumber,
        float? lapTime,
        float[]? sectorTimes,
        float? fuelUsed,
        float[]? avgTireTemps,
        float? avgSpeed,
        float? maxSpeed,
        float? avgThrottle,
        bool isValid = true,
        int incidentCount = 0)
    {
        var lapId = Guid.NewGuid().ToString();
        
        var sql = @"
            INSERT INTO LapTelemetry 
            (LapId, SetupId, LapNumber, LapTime, Sector1, Sector2, Sector3, 
             FuelUsed, AvgTireTemp_FL, AvgTireTemp_FR, AvgTireTemp_RL, AvgTireTemp_RR,
             AvgSpeed, MaxSpeed, AvgThrottle, IsValid, IncidentCount, Timestamp)
            VALUES 
            (@LapId, @SetupId, @LapNumber, @LapTime, @Sector1, @Sector2, @Sector3,
             @FuelUsed, @FL, @FR, @RL, @RR,
             @AvgSpeed, @MaxSpeed, @AvgThrottle, @IsValid, @IncidentCount, @Timestamp)
        ";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@LapId", lapId);
        command.Parameters.AddWithValue("@SetupId", setupId);
        command.Parameters.AddWithValue("@LapNumber", lapNumber);
        command.Parameters.AddWithValue("@LapTime", lapTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Sector1", sectorTimes?.ElementAtOrDefault(0) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Sector2", sectorTimes?.ElementAtOrDefault(1) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Sector3", sectorTimes?.ElementAtOrDefault(2) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FuelUsed", fuelUsed ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FL", avgTireTemps?.ElementAtOrDefault(0) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@FR", avgTireTemps?.ElementAtOrDefault(1) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RL", avgTireTemps?.ElementAtOrDefault(2) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@RR", avgTireTemps?.ElementAtOrDefault(3) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AvgSpeed", avgSpeed ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@MaxSpeed", maxSpeed ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AvgThrottle", avgThrottle ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsValid", isValid ? 1 : 0);
        command.Parameters.AddWithValue("@IncidentCount", incidentCount);
        command.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
        
        await command.ExecuteNonQueryAsync();
        return lapId;
    }
    
    /// <summary>
    /// Get laps for a setup
    /// </summary>
    public async Task<List<LapTelemetrySummary>> GetLapsAsync(string setupId, bool validOnly = false)
    {
        var sql = @"
            SELECT * FROM LapTelemetry 
            WHERE SetupId = @SetupId
              AND (@ValidOnly = 0 OR IsValid = 1)
            ORDER BY LapNumber ASC
        ";
        
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@SetupId", setupId);
        command.Parameters.AddWithValue("@ValidOnly", validOnly ? 1 : 0);
        
        var laps = new List<LapTelemetrySummary>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            laps.Add(new LapTelemetrySummary
            {
                LapId = reader.GetString(0),
                SetupId = reader.GetString(1),
                LapNumber = reader.GetInt32(2),
                LapTime = reader.IsDBNull(3) ? null : reader.GetFloat(3),
                Sector1 = reader.IsDBNull(4) ? null : reader.GetFloat(4),
                Sector2 = reader.IsDBNull(5) ? null : reader.GetFloat(5),
                Sector3 = reader.IsDBNull(6) ? null : reader.GetFloat(6),
                FuelUsed = reader.IsDBNull(7) ? null : reader.GetFloat(7),
                IsValid = reader.GetInt32(15) == 1
            });
        }
        
        return laps;
    }
    
    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}

// ===== DATA MODELS =====

public class SetupSession
{
    public string SessionId { get; set; } = "";
    public string TrackName { get; set; } = "";
    public string CarName { get; set; } = "";
    public string? SessionType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Weather { get; set; }
    public string? Notes { get; set; }
}

public class SetupConfiguration
{
    public string SetupId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string SetupName { get; set; } = "";
    public string? SetupData { get; set; }
    public string? SetupChanges { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsBaseline { get; set; }
    public int LapCount { get; set; }
    public float? BestLapTime { get; set; }
    public float? AvgLapTime { get; set; }
}

public class LapTelemetrySummary
{
    public string LapId { get; set; } = "";
    public string SetupId { get; set; } = "";
    public int LapNumber { get; set; }
    public float? LapTime { get; set; }
    public float? Sector1 { get; set; }
    public float? Sector2 { get; set; }
    public float? Sector3 { get; set; }
    public float? FuelUsed { get; set; }
    public bool IsValid { get; set; }
}
