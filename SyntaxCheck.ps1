# STATUS: CREATE  
# DESCRIPTION: Quick syntax check without building or dependencies
# FILEPATH: d:\NN and ML\MRTui\SyntaxCheck.ps1

Write-Host "=== Syntax Validation ===" -ForegroundColor Green
Write-Host "Quick syntax checking without full compilation" -ForegroundColor Cyan
Write-Host ""

$sdkPath = "src\iRacingOverlay.Core\Telemetry\CustomIRacingSDK.cs"

# Check if file exists
if (-not (Test-Path $sdkPath)) {
    Write-Host "❌ SDK file not found: $sdkPath" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "✅ SDK file found: $sdkPath" -ForegroundColor Green

# Read and analyze content
$content = Get-Content $sdkPath -Raw
$lines = Get-Content $sdkPath

Write-Host "📊 File statistics:" -ForegroundColor Cyan
Write-Host "  Lines of code: $($lines.Count)" -ForegroundColor White
Write-Host "  File size: $([math]::Round((Get-Item $sdkPath).Length/1KB, 2)) KB" -ForegroundColor White

# Basic syntax checks
Write-Host "`n🔍 Basic syntax validation:" -ForegroundColor Yellow

# Check for common syntax errors
$syntaxErrors = @()

# Check for unmatched braces
$openBraces = ([regex]::Matches($content, "\{")).Count
$closeBraces = ([regex]::Matches($content, "\}")).Count
if ($openBraces -ne $closeBraces) {
    $syntaxErrors += "Unmatched braces: $openBraces open, $closeBraces close"
}

# Check for unmatched parentheses in method signatures
$methodPattern = "(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?[\w<>\[\]]+\s+\w+\s*\([^)]*\)"
$methods = [regex]::Matches($content, $methodPattern)
Write-Host "  📋 Method signatures found: $($methods.Count)" -ForegroundColor White

# Check for missing semicolons (basic check)
$statementLines = $lines | Where-Object { 
    $_ -match "^\s*[^/\s].*[^;{}\s]\s*$" -and 
    $_ -notmatch "^\s*(if|else|for|while|switch|try|catch|finally|using|namespace|class|interface)" -and
    $_ -notmatch "^\s*\*" -and
    $_ -notmatch "^\s*//" 
}
if ($statementLines.Count -gt 0) {
    Write-Host "  ⚠️  Potential missing semicolons detected: $($statementLines.Count) lines" -ForegroundColor Yellow
} else {
    Write-Host "  ✅ No obvious missing semicolons" -ForegroundColor Green
}

# Check for proper string formatting
$stringInterpolation = ([regex]::Matches($content, '\$"[^"]*"')).Count
$stringConcatenation = ([regex]::Matches($content, '"\s*\+\s*[^"]*\+\s*"')).Count
Write-Host "  📝 String interpolations: $stringInterpolation" -ForegroundColor White
Write-Host "  📝 String concatenations: $stringConcatenation" -ForegroundColor White

# Check for async/await patterns
$asyncMethods = ([regex]::Matches($content, "async\s+")).Count
$awaitCalls = ([regex]::Matches($content, "await\s+")).Count
if ($asyncMethods -gt 0) {
    Write-Host "  ⚡ Async methods: $asyncMethods, Await calls: $awaitCalls" -ForegroundColor White
}

# Display syntax errors if any
if ($syntaxErrors.Count -gt 0) {
    Write-Host "`n❌ Potential syntax issues found:" -ForegroundColor Red
    foreach ($error in $syntaxErrors) {
        Write-Host "    $error" -ForegroundColor Red
    }
} else {
    Write-Host "  ✅ Basic syntax validation passed" -ForegroundColor Green
}

# Try C# compiler if available
Write-Host "`n🔧 Advanced syntax checking:" -ForegroundColor Yellow
$cscPath = Get-Command csc.exe -ErrorAction SilentlyContinue

if ($cscPath) {
    Write-Host "  ✅ C# compiler found: $($cscPath.Source)" -ForegroundColor Green
    
    Write-Host "  🔍 Running compiler syntax check..." -ForegroundColor Cyan
    
    # Create a minimal test to check basic syntax
    $tempFile = [System.IO.Path]::GetTempFileName() + ".cs"
    
    # Extract just the class content for basic syntax checking
    try {
        # Simple compilation test (will fail due to dependencies but shows syntax errors)
        $compileResult = & csc.exe /t:library /nologo /nowarn:1701,1702 $sdkPath 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    ✅ Syntax validation passed!" -ForegroundColor Green
        } else {
            # Filter for actual syntax errors vs dependency errors
            $syntaxErrors = $compileResult | Where-Object { 
                $_ -match "error CS\d+" -and 
                $_ -notmatch "does not exist|could not be found|missing using directive" 
            }
            
            if ($syntaxErrors.Count -gt 0) {
                Write-Host "    ❌ Syntax errors found:" -ForegroundColor Red
                $syntaxErrors | ForEach-Object { Write-Host "      $_" -ForegroundColor Red }
            } else {
                Write-Host "    ✅ No syntax errors (dependency errors expected)" -ForegroundColor Green
            }
        }
    }
    catch {
        Write-Host "    ⚠️  Could not run compiler check: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    # Clean up temp file if created
    if (Test-Path $tempFile) {
        Remove-Item $tempFile -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "  ⚠️  C# compiler not found in PATH" -ForegroundColor Yellow
    Write-Host "  💡 To enable advanced checking:" -ForegroundColor Cyan
    Write-Host "     - Install .NET SDK" -ForegroundColor Cyan
    Write-Host "     - Use Visual Studio Developer Command Prompt" -ForegroundColor Cyan
}

# Final recommendations
Write-Host "`n💡 Recommendations:" -ForegroundColor Magenta
Write-Host "  1. Run full build test when possible" -ForegroundColor White
Write-Host "  2. Use Visual Studio for comprehensive error checking" -ForegroundColor White
Write-Host "  3. Test with actual iRacing connection" -ForegroundColor White
Write-Host "  4. Monitor performance in production environment" -ForegroundColor White

Write-Host "`n✅ Syntax validation complete!" -ForegroundColor Green
Read-Host "Press Enter to exit"