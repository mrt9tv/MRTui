# Debug Settings Persistence Test
Write-Host "=== Settings Persistence Debug Test ===" -ForegroundColor Cyan
Write-Host ""

$debugLog = "$env:USERPROFILE\Documents\MRT-UI\debug.log"
$layoutFile = "$env:USERPROFILE\Documents\MRT-UI\layout.json"

# Delete old debug log
if (Test-Path $debugLog) {
    Remove-Item $debugLog
    Write-Host "✓ Cleared old debug log" -ForegroundColor Green
}

# Show current layout
if (Test-Path $layoutFile) {
    Write-Host "✓ Layout file exists" -ForegroundColor Green
    $layout = Get-Content $layoutFile | ConvertFrom-Json
    if ($layout.Widgets.Count -gt 0 -and $layout.Widgets[0].Settings.mrtone) {
        Write-Host ""
        Write-Host "Saved Settings:" -ForegroundColor Yellow
        Write-Host "  Gradient: $($layout.Widgets[0].Settings.mrtone.enableGradientBackground)" -ForegroundColor Cyan
        Write-Host "  Shift Ring: $($layout.Widgets[0].Settings.mrtone.enableShiftPointRing)" -ForegroundColor Cyan
        Write-Host "  Glow Effects: $($layout.Widgets[0].Settings.mrtone.enableGlowEffects)" -ForegroundColor Cyan
    }
} else {
    Write-Host "✗ No layout file found" -ForegroundColor Red
    Write-Host "   Please activate widget, configure, and close app first" -ForegroundColor Yellow
    exit
}

Write-Host ""
Write-Host "Starting app... Close it after checking the widget." -ForegroundColor Cyan
Write-Host "Debug log will be written to: $debugLog" -ForegroundColor Gray
Write-Host ""

# Start app
Start-Process -FilePath "dotnet" -ArgumentList "run","--project","src\iRacingOverlay.WPF" -NoNewWindow -Wait

# Show debug log
Write-Host ""
Write-Host "=== DEBUG LOG ===" -ForegroundColor Yellow
if (Test-Path $debugLog) {
    Get-Content $debugLog
} else {
    Write-Host "✗ No debug log was created!" -ForegroundColor Red
}
