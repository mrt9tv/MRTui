#!/usr/bin/env pwsh
# Build script for iRacing diagnostic tools

Write-Host "Building iRacing Diagnostic Tools" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

$ErrorActionPreference = "Stop"

try {
    # Set working directory
    Set-Location "d:\NN and ML\MRTui"
    
    Write-Host "Working directory: $(Get-Location)" -ForegroundColor Yellow
    
    # Build Core library first
    Write-Host "`nBuilding iRacingOverlay.Core..." -ForegroundColor Green
    Set-Location "src\iRacingOverlay.Core"
    dotnet build --configuration Debug
    if ($LASTEXITCODE -ne 0) { throw "Core build failed" }
    
    # Build DiagnosticTool
    Write-Host "`nBuilding DiagnosticTool..." -ForegroundColor Green
    Set-Location "..\DiagnosticTool"
    dotnet build --configuration Debug
    if ($LASTEXITCODE -ne 0) { throw "DiagnosticTool build failed" }
    
    Write-Host "`nAll projects built successfully!" -ForegroundColor Green
    Write-Host "`nReady to run diagnostic tools:" -ForegroundColor Cyan
    Write-Host "   - Comprehensive Diagnostics" -ForegroundColor White
    Write-Host "   - Live Telemetry Monitor" -ForegroundColor White
    Write-Host "   - Session State Analyzer" -ForegroundColor White
    
    Write-Host "`nTo run:" -ForegroundColor Yellow
    Write-Host "   cd src\DiagnosticTool" -ForegroundColor Gray
    Write-Host "   dotnet run" -ForegroundColor Gray
    
} catch {
    Write-Host "`nBuild failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Set-Location "d:\NN and ML\MRTui"
}
