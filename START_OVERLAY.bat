@echo off
REM iRacing Overlay Manager - WPF Application Launcher
REM Builds and runs the WPF overlay application

echo =========================================
echo iRacing Overlay Manager
echo =========================================
echo.

REM Change to WPF project directory
cd /d "f:\VSCode\Programming\MRTui\src\iRacingOverlay.WPF"

echo Building application...
dotnet build --configuration Release --nologo --verbosity quiet

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build FAILED! Check error messages above.
    pause
    exit /b 1
)

echo.
echo Starting overlay manager...
echo - Press F12 to toggle all widgets
echo - Create widgets using the manager window
echo - Close the manager window to exit
echo.

REM Run the application
dotnet run --no-build --configuration Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Application exited with errors.
    pause
    exit /b 1
)

echo.
echo Application closed normally.
pause
