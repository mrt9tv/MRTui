Write-Host "=== Full Persistence Test ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "This test verifies:" -ForegroundColor Yellow
Write-Host "  1. Opacity persists on app restart" -ForegroundColor Gray
Write-Host "  2. Size persists on app restart" -ForegroundColor Gray
Write-Host "  3. Widget stays hidden when deactivated and app restarted" -ForegroundColor Gray
Write-Host ""

$debugLog = "$env:USERPROFILE\Documents\MRT-UI\debug.log"
$layoutFile = "$env:USERPROFILE\Documents\MRT-UI\layout.json"

Write-Host "Test Sequence:" -ForegroundColor Green
Write-Host ""
Write-Host "PART 1: Test Opacity & Size Persistence" -ForegroundColor Yellow
Write-Host "  1. Start app (.\START_OVERLAY.bat)" -ForegroundColor Gray
Write-Host "  2. Activate MRT-1 widget" -ForegroundColor Gray
Write-Host "  3. Change Opacity to 50%" -ForegroundColor Gray
Write-Host "  4. Change Size to 300px" -ForegroundColor Gray
Write-Host "  5. Close app" -ForegroundColor Gray
Write-Host "  6. Restart app" -ForegroundColor Gray
Write-Host "  7. ✓ VERIFY: Widget loads with Opacity=50%, Size=300px" -ForegroundColor Cyan
Write-Host ""

Write-Host "PART 2: Test Deactivation Persistence" -ForegroundColor Yellow
Write-Host "  8. Click 'Deactivate' (widget hides)" -ForegroundColor Gray
Write-Host "  9. Close app" -ForegroundColor Gray
Write-Host "  10. Restart app" -ForegroundColor Gray
Write-Host "  11. ✓ VERIFY: Widget does NOT appear (stays deactivated)" -ForegroundColor Cyan
Write-Host "  12. Click 'Activate' (widget shows)" -ForegroundColor Gray
Write-Host "  13. ✓ VERIFY: Widget still has Opacity=50%, Size=300px" -ForegroundColor Cyan
Write-Host ""

Write-Host "After each step, check the saved data:" -ForegroundColor Green
Write-Host "  `$layout = Get-Content '$layoutFile' | ConvertFrom-Json" -ForegroundColor Gray
Write-Host "  `$layout.Widgets[0] | Format-List" -ForegroundColor Gray
Write-Host ""
Write-Host "Expected JSON structure:" -ForegroundColor Yellow
Write-Host '  "Width": 300,' -ForegroundColor Gray
Write-Host '  "Height": 300,' -ForegroundColor Gray
Write-Host '  "Opacity": 0.5,' -ForegroundColor Gray
Write-Host '  "IsVisible": false,' -ForegroundColor Gray
Write-Host ""
