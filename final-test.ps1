# Final Settings Persistence Test
Write-Host "=== FINAL PERSISTENCE TEST ===" -ForegroundColor Cyan
Write-Host ""

$debugLog = "$env:USERPROFILE\Documents\MRT-UI\debug.log"
$layoutFile = "$env:USERPROFILE\Documents\MRT-UI\layout.json"

# Clear debug log
if (Test-Path $debugLog) { Remove-Item $debugLog }

# Show what's saved
if (Test-Path $layoutFile) {
    Write-Host "Current saved settings:" -ForegroundColor Yellow
    $layout = Get-Content $layoutFile | ConvertFrom-Json
    if ($layout.Widgets.Count -gt 0 -and $layout.Widgets[0].Settings.mrtone) {
        Write-Host "  Gradient: $($layout.Widgets[0].Settings.mrtone.enableGradientBackground)" -ForegroundColor Cyan
        Write-Host "  Shift Ring: $($layout.Widgets[0].Settings.mrtone.enableShiftPointRing)" -ForegroundColor Cyan
        Write-Host "  Glow: $($layout.Widgets[0].Settings.mrtone.enableGlowEffects)" -ForegroundColor Cyan
    }
} else {
    Write-Host "No layout file - activate widget first" -ForegroundColor Red
    exit
}

Write-Host ""
Write-Host "Starting app... Check if widget appears with shift ring enabled!" -ForegroundColor Green
Write-Host "Close the app when done." -ForegroundColor Yellow
Write-Host ""

# Run app
dotnet run --project src\iRacingOverlay.WPF

# Show results
Write-Host ""
Write-Host "=== DEBUG LOG ===" -ForegroundColor Yellow
if (Test-Path $debugLog) {
    $content = Get-Content $debugLog -Raw
    Write-Host $content
    
    # Check for success
    if ($content -match "✓ SUCCESS - ShiftRing=True") {
        Write-Host ""
        Write-Host "✓✓✓ SETTINGS LOADED SUCCESSFULLY! ✓✓✓" -ForegroundColor Green
    } elseif ($content -match "Returning DEFAULTS") {
        Write-Host ""
        Write-Host "✗✗✗ SETTINGS NOT LOADED - Using defaults ✗✗✗" -ForegroundColor Red
    }
} else {
    Write-Host "No debug log created" -ForegroundColor Red
}
