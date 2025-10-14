@echo off
REM iRacing Telemetry Overlay Launcher
REM This opens a new console window to display telemetry data

title iRacing Telemetry Overlay

echo ===============================================
echo iRacing Telemetry Overlay
echo ===============================================
echo.
echo Starting application...
echo.

cd /d "%~dp0src\iRacingOverlay.Core"
dotnet run --configuration Release

pause
