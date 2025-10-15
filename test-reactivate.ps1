# Test Deactivate/Reactivate Persistence
Write-Host "=== Deactivate/Reactivate Test ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Steps to test:" -ForegroundColor Yellow
Write-Host "  1. Open app" -ForegroundColor White
Write-Host "  2. Activate widget (should load with shift ring ON)" -ForegroundColor White
Write-Host "  3. Deactivate widget" -ForegroundColor White
Write-Host "  4. Activate widget AGAIN" -ForegroundColor White
Write-Host "  5. Verify shift ring is STILL enabled" -ForegroundColor Green
Write-Host ""
Write-Host "Starting app..." -ForegroundColor Cyan
Write-Host ""

dotnet run --project src\iRacingOverlay.WPF
