@echo off
REM ===============================================
REM iRacing Telemetry Overlay - Standalone Launcher
REM ===============================================
REM
REM Usage:
REM   START_TELEMETRY.bat           - Full display (default)
REM   START_TELEMETRY.bat --quiet   - Minimal single-line output
REM   START_TELEMETRY.bat --verbose - Extra debug information
REM
REM ===============================================

cd /d "%~dp0build"

REM Set console window properties for better visibility
title iRacing Telemetry Overlay
mode con: cols=80 lines=35
color 0A

REM Run the executable with any passed arguments
iRacingOverlay.Core.exe %*

REM Pause if there's an error
if errorlevel 1 (
    echo.
    echo An error occurred. Press any key to exit...
    pause >nul
)
