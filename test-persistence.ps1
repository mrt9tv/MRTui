# Test script for settings persistence
Write-Host "=== Settings Persistence Debug Test ===" -ForegroundColor Cyan
Write-Host ""

# Check if layout file exists
$layoutPath = "$env:USERPROFILE\Documents\MRT-UI\layout.json"
if (Test-Path $layoutPath) {
    Write-Host "✓ Layout file found" -ForegroundColor Green
    Write-Host ""
    Write-Host "=== Saved Layout Content ===" -ForegroundColor Yellow
    $content = Get-Content $layoutPath -Raw
    Write-Host $content
    Write-Host ""
    Write-Host "=== Parsed Settings ===" -ForegroundColor Yellow
    $layout = $content | ConvertFrom-Json
    if ($layout.Widgets.Count -gt 0) {
        $widget = $layout.Widgets[0]
        Write-Host "Widget Type: $($widget.Type)" -ForegroundColor Cyan
        Write-Host "Position: X=$($widget.X), Y=$($widget.Y)" -ForegroundColor Cyan
        Write-Host "Size: $($widget.Width) x $($widget.Height)" -ForegroundColor Cyan
        if ($widget.Settings.mrtone) {
            Write-Host ""
            Write-Host "MRT One Settings:" -ForegroundColor Green
            Write-Host "  EnableGradientBackground: $($widget.Settings.mrtone.enableGradientBackground)" -ForegroundColor Green
            Write-Host "  EnableShiftPointRing: $($widget.Settings.mrtone.enableShiftPointRing)" -ForegroundColor Green
            Write-Host "  EnableGlowEffects: $($widget.Settings.mrtone.enableGlowEffects)" -ForegroundColor Green
        }
    }
    Write-Host ""
} else {
    Write-Host "✗ No layout file found at: $layoutPath" -ForegroundColor Red
    Write-Host "   Please activate a widget, configure settings, and close the app first." -ForegroundColor Yellow
    Write-Host ""
    exit
}

Write-Host "Press ENTER to start the application and watch debug output..." -ForegroundColor Cyan
Read-Host

Write-Host ""
Write-Host "=== Starting Application ===" -ForegroundColor Cyan
Write-Host "Watch for these debug messages:" -ForegroundColor Yellow
Write-Host "  - 'LoadSavedLayout called'" -ForegroundColor Gray
Write-Host "  - 'Loading widget config'" -ForegroundColor Gray  
Write-Host "  - 'MRTOneWidget.LoadSettings'" -ForegroundColor Gray
Write-Host "  - 'ApplyVisualEnhancements'" -ForegroundColor Gray
Write-Host ""

# Start the app (will run in foreground)
dotnet run --project src\iRacingOverlay.WPF
