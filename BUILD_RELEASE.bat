@echo off
echo ============================================
echo   MRT-UI Release Build
echo ============================================
echo.

:: Clean previous build
if exist "%~dp0build\release" (
    echo Cleaning previous build...
    rmdir /s /q "%~dp0build\release"
)

:: Publish
echo Building Release...
dotnet publish "%~dp0src\iRacingOverlay.WPF\iRacingOverlay.WPF.csproj" ^
    -c Release ^
    -r win-x64 ^
    --no-self-contained ^
    -o "%~dp0build\release" ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED!
    pause
    exit /b 1
)

echo.
echo ============================================
echo   Build complete!
echo   Output: build\release\
echo ============================================
echo.

:: Copy TrackTurnDatabase
if exist "%~dp0src\iRacingOverlay.Core\Data\TrackTurnDatabase.json" (
    if not exist "%~dp0build\release\Data" mkdir "%~dp0build\release\Data"
    copy "%~dp0src\iRacingOverlay.Core\Data\TrackTurnDatabase.json" "%~dp0build\release\Data\" >nul
    echo Copied TrackTurnDatabase.json
)

echo.
echo To run: build\release\iRacingOverlay.WPF.exe
pause
