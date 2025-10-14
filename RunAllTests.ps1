# STATUS: CREATE
# DESCRIPTION: Quick start batch script to run all validation tests
# FILEPATH: d:\NN and ML\MRTui\RunAllTests.ps1

Write-Host "=== iRacing Telemetry Improvement Validation ===" -ForegroundColor Green
Write-Host "Running all validation tests without NuGet dependencies" -ForegroundColor Cyan
Write-Host ""

# Set execution policy for current session if needed
try {
    Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
    Write-Host "✅ PowerShell execution policy set" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Could not set execution policy: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`n📋 Available Tests:" -ForegroundColor Yellow
Write-Host "  1. PowerShell Direct Testing" -ForegroundColor White
Write-Host "  2. Manual Code Review" -ForegroundColor White  
Write-Host "  3. Syntax Validation" -ForegroundColor White
Write-Host "  4. Run All Tests" -ForegroundColor White
Write-Host "  5. Exit" -ForegroundColor White

do {
    Write-Host "`nSelect test to run (1-5): " -ForegroundColor Cyan -NoNewline
    $choice = Read-Host
    
    switch ($choice) {
        "1" {
            Write-Host "`n🔄 Running PowerShell Direct Testing..." -ForegroundColor Green
            if (Test-Path "TestTelemetry.ps1") {
                & .\TestTelemetry.ps1
            } else {
                Write-Host "❌ TestTelemetry.ps1 not found. Please create it first." -ForegroundColor Red
            }
        }
        "2" {
            Write-Host "`n🔄 Running Manual Code Review..." -ForegroundColor Green
            if (Test-Path "ManualValidation.ps1") {
                & .\ManualValidation.ps1
            } else {
                Write-Host "❌ ManualValidation.ps1 not found. Please create it first." -ForegroundColor Red
            }
        }
        "3" {
            Write-Host "`n🔄 Running Syntax Validation..." -ForegroundColor Green
            if (Test-Path "SyntaxCheck.ps1") {
                & .\SyntaxCheck.ps1
            } else {
                Write-Host "❌ SyntaxCheck.ps1 not found. Please create it first." -ForegroundColor Red
            }
        }
        "4" {
            Write-Host "`n🔄 Running All Tests..." -ForegroundColor Green
            
            $tests = @("TestTelemetry.ps1", "ManualValidation.ps1", "SyntaxCheck.ps1")
            foreach ($test in $tests) {
                if (Test-Path $test) {
                    Write-Host "`n--- Running $test ---" -ForegroundColor Magenta
                    & .\$test
                    Write-Host "--- $test Complete ---`n" -ForegroundColor Magenta
                } else {
                    Write-Host "⚠️  $test not found, skipping..." -ForegroundColor Yellow
                }
            }
            
            Write-Host "🎉 All available tests completed!" -ForegroundColor Green
        }
        "5" {
            Write-Host "👋 Exiting..." -ForegroundColor Green
            exit
        }
        default {
            Write-Host "❌ Invalid choice. Please select 1-5." -ForegroundColor Red
        }
    }
} while ($choice -ne "5")