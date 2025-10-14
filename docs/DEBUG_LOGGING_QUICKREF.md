# Debug Logging Quick Reference

## 🚀 Quick Start

```batch
START_OVERLAY_DEBUG.bat
```

## 📄 Log File Location

```
src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\logs\debug_YYYYMMDD_HHMMSS.log
```

## 🔍 What to Check in Logs

### ✅ Connection Working
```
[INFO ] ConnectionStatusService: Status changing from Disconnected to Connected
[DEBUG] IRacingTelemetryService: OnTelemetryReceived called
[DEBUG] ConnectionStatusService: Telemetry rate: 60Hz
```

### ❌ Connection Not Working
```
[DEBUG] IRacingTelemetryService: iRacing not running
[WARN ] ConnectionStatusService: No telemetry received in 5 seconds
```

### ✅ RPM Colors Working
```
[DEBUG] ShiftPointCalculator: Set Shift RPM: 8500
[DEBUG] ShiftPointCalculator: Set Blink RPM: 8700
[DEBUG] ShiftPointCalculator: Using SDK zones - Shift: 8500, Blink: 8700
```

### ❌ RPM Colors Not Working
```
[DEBUG] ShiftPointCalculator: Using FALLBACK zones - SDK Shift: 0, SDK Blink: 0
```
→ **Issue:** SDK RPM values are 0, check YAML parsing

### ✅ DataWidget Fields Working
```
[DEBUG] DataWidget: Field: RPM, Value: 1200, Type: Single
[DEBUG] DataWidget: Field: Speed, Value: 45.5, Type: Single
[DEBUG] DataWidget: Field: TireWearAll, Value: FL 100% 100% RF..., Type: String
```

### ❌ DataWidget Fields Not Working
```
[DEBUG] DataWidget: Field: FuelLevel, Value: NULL, Type: NULL
```
→ **Issue:** TelemetryDataMapper returning null, check SDK field mapping

## 📊 Common Issues & Log Patterns

| Issue | Log Pattern | Solution |
|-------|-------------|----------|
| No telemetry | `iRacing not running` | Start iRacing and enter session |
| Low Hz rate | `Telemetry rate: 20Hz` | Check iRacing SDK connection |
| Missing SDK values | `Set Shift RPM: 0` | Verify YAML parsing at connection |
| Widget shows "---" | `Value: NULL` | Check field mapping in TelemetryDataMapper |
| No color zones | `Using FALLBACK zones` | SDK values missing, check car compatibility |

## 🛠️ Log Analysis Commands

### View latest log
```batch
type src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\logs\debug_*.log | more
```

### Search for errors
```powershell
Select-String -Path "src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\logs\*.log" -Pattern "\[ERROR\]"
```

### Filter by component
```powershell
Select-String -Path "src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\logs\*.log" -Pattern "DataWidget:"
```

## 📁 Log File Cleanup

```batch
# Delete all logs
del /Q src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\logs\*.log

# Or keep latest 5 logs
cd src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\logs
for /f "skip=5 delims=" %F in ('dir /b /o-d debug_*.log') do @del "%F"
```

## 📝 Bug Report Template

```
Issue: [Brief description]
Log File: debug_YYYYMMDD_HHMMSS.log (attached)
Steps: 
  1. [What you did]
  2. [What happened]
Expected: [What should happen]
Actual: [What actually happened]
Key Log Lines:
  [Paste relevant log entries showing issue]
```
