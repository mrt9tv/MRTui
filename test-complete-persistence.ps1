Write-Host "=== Complete Settings Persistence Test ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Architecture:" -ForegroundColor Yellow
Write-Host "  - Each widget manages its own settings (MRTOneSettings)"
Write-Host "  - Widget saves settings to Config.Settings['mrtone'] on changes"
Write-Host "  - WidgetManager saves complete layout (all configs) to layout.json"
Write-Host "  - Deactivate = Hide widget (keeps in memory with settings)"
Write-Host "  - Activate = Show existing widget OR create new one with saved config"
Write-Host ""

$debugLog = "$env:USERPROFILE\Documents\MRT-UI\debug.log"
$layoutFile = "$env:USERPROFILE\Documents\MRT-UI\layout.json"

Write-Host "1. Clear debug log" -ForegroundColor Green
if (Test-Path $debugLog) {
    Remove-Item $debugLog
    Write-Host "   ✓ Cleared" -ForegroundColor Gray
}

Write-Host ""
Write-Host "2. Starting the application..." -ForegroundColor Green
Start-Process "F:\VSCode\Programming\MRTui\src\iRacingOverlay.WPF\bin\Debug\net8.0-windows\iRacingOverlay.WPF.exe"
Start-Sleep -Seconds 2
Write-Host "   ✓ Application launched" -ForegroundColor Gray
Write-Host ""
Write-Host "   - Click 'Activate' on MRT-1 widget" -ForegroundColor Gray
Write-Host "   - Verify: Widget appears with gradient ON, shift ring OFF (defaults)" -ForegroundColor Gray
Write-Host ""

Write-Host "3. Change settings" -ForegroundColor Green
Write-Host "   - Toggle Shift Ring = ON" -ForegroundColor Gray
Write-Host "   - Toggle Glow Effects = ON" -ForegroundColor Gray
Write-Host "   - Verify: Visual changes appear immediately" -ForegroundColor Gray
Write-Host ""

Write-Host "4. Test Deactivate/Activate (in-memory persistence)" -ForegroundColor Green
Write-Host "   - Click 'Deactivate' button" -ForegroundColor Gray
Write-Host "   - Verify: Widget disappears, button shows 'Activate'" -ForegroundColor Gray
Write-Host "   - Click 'Activate' button" -ForegroundColor Gray
Write-Host "   - Verify: Widget reappears with Shift Ring ON, Glow ON ✓" -ForegroundColor Yellow
Write-Host ""

Write-Host "5. Test App Restart (disk persistence)" -ForegroundColor Green
Write-Host "   - Close the application" -ForegroundColor Gray
Write-Host "   - Start the application again" -ForegroundColor Gray
Write-Host "   - Verify: Widget loads automatically with Shift Ring ON, Glow ON ✓" -ForegroundColor Yellow
Write-Host ""

Write-Host "6. Check saved data" -ForegroundColor Green
Write-Host "   - Run: Get-Content '$layoutFile' | ConvertFrom-Json | ConvertTo-Json -Depth 10" -ForegroundColor Gray
Write-Host "   - Verify: Settings.mrtone contains all Phase 2 properties" -ForegroundColor Gray
Write-Host ""

Write-Host "After testing, check logs:" -ForegroundColor Cyan
Write-Host "  Get-Content '$debugLog' -Tail 100" -ForegroundColor Gray
Write-Host ""
Write-Host "Look for:" -ForegroundColor Yellow
Write-Host "  [OverlayVM] IsActive: Widget exists, showing it" -ForegroundColor Gray
Write-Host "  [MRTOne] LoadSettings: ✓ Found 'mrtone' entry" -ForegroundColor Gray
Write-Host "  [MRTOne] ApplyVisualEnhancements: ShiftRing=True, Glow=True" -ForegroundColor Gray
Write-Host ""
