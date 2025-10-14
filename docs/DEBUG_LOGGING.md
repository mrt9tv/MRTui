# Debug Logging System

## Overview

The iRacing Overlay Manager includes a comprehensive debug logging system that automatically saves session logs to timestamped files when running in debug mode.

## Log File Location

Debug logs are saved to:
```
src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\logs\debug_YYYYMMDD_HHMMSS.log
```

**Example:**
```
logs\debug_20241013_153042.log  (Started October 13, 2024 at 3:30:42 PM)
```

## Using Debug Mode

### Option 1: Use the Debug Batch File (Recommended)

Simply run:
```batch
START_OVERLAY_DEBUG.bat
```

This automatically:
- ✅ Builds in Debug configuration
- ✅ Enables full debug logging to console
- ✅ Creates timestamped log file in `logs/` directory
- ✅ Shows log file location on exit

### Option 2: Manual Debug Mode

Set environment variable before running:
```batch
set IRACING_OVERLAY_LOG_LEVEL=Debug
dotnet run --configuration Debug --project src\iRacingOverlay.WPF
```

## Log File Format

Each log entry includes:
- **Timestamp** (HH:mm:ss.fff) - Millisecond precision
- **Log Level** (TRACE/DEBUG/INFO/WARN/ERROR/CRIT)
- **Category** (e.g., `iRacingOverlay.Core.Services.IRacingTelemetryService`)
- **Message** (the actual log content)

**Example log entries:**
```
[15:30:42.123] [DEBUG] iRacingOverlay.Core.Services.IRacingTelemetryService: OnTelemetryReceived called
[15:30:42.124] [INFO ] iRacingOverlay.Core.Services.ConnectionStatusService: Status changing from Disconnected to Connected
[15:30:42.125] [DEBUG] iRacingOverlay.WPF.Widgets.DataWidget.DataWidget: Update #1 - RPM: 1200, Gear: 0
[15:30:42.126] [DEBUG] iRacingOverlay.WPF.Utils.ShiftPointCalculator: Set Shift RPM: 8500
```

## What Gets Logged

### Connection Status
- SDK connection state changes (Disconnected → Searching → Connected)
- Telemetry rate monitoring (Hz updates every second)
- Track/driver information updates
- Connection health checks

### Telemetry Data
- RPM, Gear, Speed, Throttle, Brake values
- Shift point calculator tracking (SDK RPM values)
- Widget field updates (first 5 updates per session)
- Temperature, fuel, tire data processing

### Widget Operations
- Widget creation/removal events
- UpdateUI() calls with current data
- Field value retrieval and formatting
- Color zone calculations (RPM, temperature, fuel)

### System Events
- Application startup/shutdown
- Dependency injection registration
- Service initialization
- Event subscription tracking

## Log File Management

### Automatic Cleanup
- **Keep:** Log files are NOT automatically deleted
- **Manual:** Review and delete old logs from `logs/` directory as needed

### Storage Recommendations
- Each session typically generates 1-5 MB of logs
- Review logs after each debugging session
- Archive important debugging sessions separately
- Delete routine logs after issues are resolved

### Example Cleanup
```batch
# Delete logs older than 7 days (PowerShell)
Get-ChildItem -Path "src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\logs" -Filter "debug_*.log" | 
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } | 
  Remove-Item

# Or manually delete all logs
del /Q src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\logs\*.log
```

## Troubleshooting

### Log File Not Created
**Problem:** No log file appears in `logs/` directory

**Solutions:**
1. Verify you're using `START_OVERLAY_DEBUG.bat` or Debug build configuration
2. Check console output for "DEBUG LOG FILE:" message showing path
3. Ensure application has write permissions to directory
4. Check `bin\Debug\net8.0-windows\` directory exists

### Log File Empty
**Problem:** Log file created but contains no logs

**Solutions:**
1. Verify environment variable: `IRACING_OVERLAY_LOG_LEVEL=Debug`
2. Check console shows "DEBUG LOGGING ENABLED" message
3. Ensure application ran long enough to generate logs
4. Check for file permissions issues

### Missing Debug Messages
**Problem:** Some expected log messages don't appear

**Solutions:**
1. Verify log level is set to `Debug` (not `Information`)
2. Check specific component has debug logging enabled
3. Some widgets only log first 5 updates to reduce log spam
4. Review filter settings in logging configuration

## Log Analysis Tips

### Finding Connection Issues
Search for:
```
"Status changing from Disconnected"
"OnTelemetryReceived called"
"Telemetry rate: XXHz"
```

### Finding RPM Color Zone Issues
Search for:
```
"[ShiftPointCalculator]"
"Set Shift RPM:"
"Using SDK zones" or "Using FALLBACK zones"
"Update #1 - RPM:"
```

### Finding Widget Data Issues
Search for:
```
"[DataWidget] Field:"
"Value: NULL"
"Type: String" or "Type: Single"
```

### Finding Errors
Search for:
```
"[ERROR]"
"[CRIT ]"
"Exception:"
```

## Performance Impact

### Debug Mode Overhead
- **Console Output:** Minimal (~1-2% CPU)
- **File Logging:** Minimal (~1-2% CPU, buffered writes)
- **Memory:** ~5-10 MB additional for logging buffers
- **Disk I/O:** AutoFlush enabled, minimal impact on SSD

### Production Mode (Release Build)
- File logging automatically disabled
- Console logging reduced to `Information` level only
- Minimal performance overhead

## Best Practices

1. **Always use debug mode when reporting bugs** - Include the log file with issue reports
2. **Review logs after each session** - Identify patterns in errors or warnings
3. **Archive important debugging sessions** - Copy logs to separate folder before cleanup
4. **Check log file location on startup** - Console shows exact path to log file
5. **Delete old logs regularly** - Prevent log directory from growing too large

## Integration with Bug Reports

When reporting issues, always include:
1. **Log file** from the debug session showing the problem
2. **Screenshot** of the issue (if visual)
3. **Steps to reproduce** - What you did before the issue occurred
4. **iRacing car/track** - What car and track you were using
5. **System info** - Windows version, .NET version

**Example bug report:**
```
Issue: RPM colors not working in DataWidget
Log File: debug_20241013_153042.log (attached)
Screenshot: rpm_no_color.png (attached)
Steps:
  1. Started overlay with START_OVERLAY_DEBUG.bat
  2. Loaded iRacing with Porsche 911 GT3 R at Spa
  3. DataWidget RPM cell shows correct values but stays teal
Logs show:
  [15:30:45.789] [DEBUG] ShiftPointCalculator: Set Shift RPM: 0
  [15:30:45.790] [DEBUG] ShiftPointCalculator: Using FALLBACK zones
  -> SDK values are 0, should investigate YAML parsing
```

## Advanced: Custom Log Filtering

To filter logs by component, search for specific category names:

| Component | Category Name |
|-----------|---------------|
| Telemetry Service | `iRacingOverlay.Core.Services.IRacingTelemetryService` |
| Connection Status | `iRacingOverlay.Core.Services.ConnectionStatusService` |
| DataWidget | `iRacingOverlay.WPF.Widgets.DataWidget.DataWidget` |
| GearGaugeWidget | `iRacingOverlay.WPF.Widgets.GearGaugeWidget.GearGaugeWidget` |
| FuelWidget | `iRacingOverlay.WPF.Widgets.FuelWidget.FuelWidget` |
| ShiftPointCalculator | `iRacingOverlay.WPF.Utils.ShiftPointCalculator` |
| WidgetManager | `iRacingOverlay.WPF.Services.WidgetManager` |

**Example PowerShell filter:**
```powershell
# Show only DataWidget logs
Select-String -Path "logs\debug_*.log" -Pattern "DataWidget:" | Out-File filtered_datawidget.log

# Show only ERROR and CRIT logs
Select-String -Path "logs\debug_*.log" -Pattern "\[(ERROR|CRIT )\]" | Out-File errors_only.log
```

## See Also

- [DEBUG-QUICK-START.md](DEBUG-QUICK-START.md) - Quick debugging guide
- [WPF_TESTING_GUIDE.md](WPF_TESTING_GUIDE.md) - Testing procedures
- [PROJECT_STATUS.md](PROJECT_STATUS.md) - Current development status
