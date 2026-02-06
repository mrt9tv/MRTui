@echo off
REM MRT One - iRacing Overlay Launcher
REM Builds and runs the WPF overlay application

echo =========================================
echo MRT One - iRacing Overlay
echo =========================================
echo.

REM Change to script directory
cd /d "%~dp0"

echo Building application...
dotnet build src\iRacingOverlay.WPF\iRacingOverlay.WPF.csproj --configuration Release --nologo --verbosity quiet

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build FAILED! Check error messages above.
    pause
    exit /b 1
)

echo.
echo Starting MRT One overlay...
echo - Press F12 to toggle all widgets
echo - Press F11 to lock/unlock widgets
echo - Close the manager window to exit
echo.

REM Run the application
dotnet run --project src\iRacingOverlay.WPF\iRacingOverlay.WPF.csproj --no-build --configuration Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Application exited with errors.
    pause
    exit /b 1
)

echo.
echo Application closed normally.
pause
