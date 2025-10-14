@echo off
REM iRacing Overlay Manager - DEBUG MODE Launcher
REM Builds and runs with detailed logging for troubleshooting

echo =========================================
echo iRacing Overlay Manager - DEBUG MODE
echo =========================================
echo.

REM Change to project root
cd /d "f:\VSCode\Programming\MRTui"

REM Rotate log files (keep last 3)
if exist "debug-3.log" del "debug-3.log"
if exist "debug-2.log" ren "debug-2.log" "debug-3.log"
if exist "debug-1.log" ren "debug-1.log" "debug-2.log"
if exist "debug.log" ren "debug.log" "debug-1.log"

echo Creating new debug.log file...
echo.

REM Build with detailed output
echo [DEBUG] Building application with detailed verbosity... > debug.log
echo Build started at %date% %time% >> debug.log
echo. >> debug.log

dotnet build iRacingOverlay.sln --configuration Debug --verbosity detailed >> debug.log 2>&1

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build FAILED! Check debug.log for details.
    echo Last 20 lines of debug.log:
    echo ----------------------------------------
    powershell -Command "Get-Content debug.log -Tail 20"
    echo ----------------------------------------
    pause
    exit /b 1
)

echo [DEBUG] Build succeeded! >> debug.log
echo. >> debug.log
echo [DEBUG] Starting application... >> debug.log
echo Application started at %date% %time% >> debug.log
echo. >> debug.log

echo Build successful!
echo Starting overlay manager in DEBUG mode...
echo - Logging to: debug.log
echo - Press F12 to toggle all widgets
echo - Close the manager window to exit
echo.

REM Run the application with error output captured
cd src\iRacingOverlay.WPF
dotnet run --no-build --configuration Debug >> ..\..\debug.log 2>&1

set EXIT_CODE=%ERRORLEVEL%

cd ..\..

echo. >> debug.log
echo Application exited at %date% %time% with code %EXIT_CODE% >> debug.log

if %EXIT_CODE% NEQ 0 (
    echo.
    echo [ERROR] Application exited with errors (code %EXIT_CODE%)
    echo Last 30 lines of debug.log:
    echo ----------------------------------------
    powershell -Command "Get-Content debug.log -Tail 30"
    echo ----------------------------------------
    pause
    exit /b %EXIT_CODE%
)

echo.
echo Application closed normally.
echo Debug log saved to: debug.log
pause
