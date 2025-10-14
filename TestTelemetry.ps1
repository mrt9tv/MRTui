# STATUS: CREATE
# DESCRIPTION: PowerShell script to test compilation and validate improvements
# FILEPATH: d:\NN and ML\MRTui\TestTelemetry.ps1

# Set working directory
Set-Location "d:\NN and ML\MRTui"

Write-Host "=== iRacing Telemetry Validation Test ===" -ForegroundColor Green
Write-Host "Testing improvements without NuGet dependencies" -ForegroundColor Cyan
Write-Host ""

# Check if SDK file exists
$sdkPath = "src\iRacingOverlay.Core\Telemetry\CustomIRacingSDK.cs"
if (Test-Path $sdkPath) {
    Write-Host "✅ SDK file found: $sdkPath" -ForegroundColor Green
    
    # Get file information
    $fileInfo = Get-Item $sdkPath
    Write-Host "📁 File size: $([math]::Round($fileInfo.Length/1KB, 2)) KB" -ForegroundColor Cyan
    Write-Host "📅 Last modified: $($fileInfo.LastWriteTime)" -ForegroundColor Cyan
    
    # Read file content
    $content = Get-Content $sdkPath -Raw
    
    Write-Host "`n🔍 Checking for implemented methods:" -ForegroundColor Yellow
    
    # Check for new methods
    $methods = @(
        @{Name="IsSessionStateValid"; Type="Session validation"},
        @{Name="IsBufferFresh"; Type="Buffer freshness checking"},
        @{Name="HasValidTelemetryData"; Type="Multi-variable validation"},
        @{Name="DiagnoseConnection"; Type="Connection diagnostics"},
        @{Name="LogAllAvailableVariables"; Type="Variable logging"}
    )
    
    $foundMethods = 0
    foreach ($method in $methods) {
        if ($content -match "public.*$($method.Name)\s*\(|private.*$($method.Name)\s*\(") {
            Write-Host "  ✅ $($method.Name) - $($method.Type)" -ForegroundColor Green
            $foundMethods++
        } else {
            Write-Host "  ❌ $($method.Name) - Missing" -ForegroundColor Red
        }
    }
    
    # Check for the new field
    Write-Host "`n🔧 Checking for new fields:" -ForegroundColor Yellow
    if ($content -match "_lastValidTick") {
        Write-Host "  ✅ _lastValidTick field - Found" -ForegroundColor Green
    } else {
        Write-Host "  ❌ _lastValidTick field - Missing" -ForegroundColor Red
    }
    
    # Check for integration improvements
    Write-Host "`n🔗 Checking for integration improvements:" -ForegroundColor Yellow
    
    $integrationChecks = @(
        @{Pattern="IsBufferFresh\(\)"; Description="Buffer freshness in IsDataAvailable"},
        @{Pattern="HasValidTelemetryData\(\)"; Description="Multi-variable validation"},
        @{Pattern="DiagnoseConnection\(\)"; Description="Diagnostic integration"}
    )
    
    foreach ($check in $integrationChecks) {
        if ($content -match $check.Pattern) {
            Write-Host "  ✅ $($check.Description)" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  $($check.Description) - Not detected" -ForegroundColor Yellow
        }
    }
    
    # Error handling analysis
    Write-Host "`n🛡️ Error handling analysis:" -ForegroundColor Yellow
    $tryBlocks = ([regex]::Matches($content, "try\s*\{")).Count
    $catchBlocks = ([regex]::Matches($content, "catch\s*\(")).Count
    $loggingStatements = ([regex]::Matches($content, "Console\.WriteLine|Debug\.WriteLine|Log\.|logger\.")).Count
    
    Write-Host "  📊 Try blocks: $tryBlocks" -ForegroundColor Cyan
    Write-Host "  📊 Catch blocks: $catchBlocks" -ForegroundColor Cyan
    Write-Host "  📊 Logging statements: $loggingStatements" -ForegroundColor Cyan
    
    # Summary
    Write-Host "`n📋 Implementation Summary:" -ForegroundColor Magenta
    Write-Host "  Methods implemented: $foundMethods/5" -ForegroundColor $(if($foundMethods -eq 5){'Green'}else{'Yellow'})
    Write-Host "  Error handling: $(if($tryBlocks -gt 0 -and $catchBlocks -gt 0){'✅ Present'}else{'⚠️ Limited'})" -ForegroundColor $(if($tryBlocks -gt 0 -and $catchBlocks -gt 0){'Green'}else{'Yellow'})
    Write-Host "  Logging: $(if($loggingStatements -gt 10){'✅ Comprehensive'}else{'⚠️ Basic'})" -ForegroundColor $(if($loggingStatements -gt 10){'Green'}else{'Yellow'})
    
} else {
    Write-Host "❌ SDK file not found: $sdkPath" -ForegroundColor Red
    Write-Host "Please verify the project structure." -ForegroundColor Yellow
}

# Check for backup file
Write-Host "`n💾 Backup file status:" -ForegroundColor Yellow
$backupPath = "src\iRacingOverlay.Core\Telemetry\CustomIRacingSDK.cs.backup"
if (Test-Path $backupPath) {
    Write-Host "  ✅ Backup file exists: $backupPath" -ForegroundColor Green
} else {
    Write-Host "  ⚠️  No backup file found" -ForegroundColor Yellow
}

# Check for documentation
Write-Host "`n📚 Documentation status:" -ForegroundColor Yellow
$docsToCheck = @("IMPROVEMENTS_SUMMARY.md", "fixmaybe.md")
foreach ($doc in $docsToCheck) {
    if (Test-Path $doc) {
        Write-Host "  ✅ $doc - Found" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  $doc - Not found" -ForegroundColor Yellow
    }
}

Write-Host "`n📊 Analysis complete!" -ForegroundColor Green
Write-Host "Press Enter to continue with syntax validation..." -ForegroundColor Cyan
Read-Host

# Basic syntax validation (if C# compiler available)
Write-Host "`n🔍 Attempting basic syntax validation..." -ForegroundColor Yellow
$cscPath = Get-Command csc.exe -ErrorAction SilentlyContinue
if ($cscPath) {
    Write-Host "✅ C# compiler found, running syntax check..." -ForegroundColor Green
    
    try {
        # Try to compile syntax only (will fail due to dependencies but shows syntax errors)
        $output = csc.exe /t:library /nologo /define:SKIP_DEPENDENCIES $sdkPath 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Basic syntax validation passed!" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Syntax issues detected (may include dependency errors):" -ForegroundColor Yellow
            $output | Where-Object { $_ -match "error CS" } | ForEach-Object { 
                Write-Host "    $_" -ForegroundColor Red 
            }
        }
    } catch {
        Write-Host "⚠️  Could not run syntax validation: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️  C# compiler not found in PATH" -ForegroundColor Yellow
    Write-Host "💡 To enable syntax checking, install .NET SDK" -ForegroundColor Cyan
}

Write-Host "`nPress Enter to exit..." -ForegroundColor Cyan
Read-Host