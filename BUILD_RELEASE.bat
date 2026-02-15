@echo off
echo ============================================
echo   MRT-UI Release Build + Velopack Package
echo ============================================
echo.

:: Clean previous build
if exist "%~dp0build\release" (
    echo Cleaning previous build...
    rmdir /s /q "%~dp0build\release"
)
if exist "%~dp0build\publish" (
    rmdir /s /q "%~dp0build\publish"
)

:: Clean obj/bin to prevent stale artifact OOM
echo Cleaning intermediate build files...
if exist "%~dp0src\iRacingOverlay.Core\bin" rmdir /s /q "%~dp0src\iRacingOverlay.Core\bin"
if exist "%~dp0src\iRacingOverlay.Core\obj" rmdir /s /q "%~dp0src\iRacingOverlay.Core\obj"
if exist "%~dp0src\iRacingOverlay.WPF\bin" rmdir /s /q "%~dp0src\iRacingOverlay.WPF\bin"
if exist "%~dp0src\iRacingOverlay.WPF\obj" rmdir /s /q "%~dp0src\iRacingOverlay.WPF\obj"

:: Publish (single-threaded to reduce peak memory — avoids OOM with other apps running)
echo Building Release...
dotnet publish "%~dp0src\iRacingOverlay.WPF\iRacingOverlay.WPF.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained ^
    -o "%~dp0build\publish" ^
    /p:PublishSingleFile=true ^
    /p:PublishReadyToRun=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    -m:1

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED!
    pause
    exit /b 1
)

echo.
echo Build complete. Preparing Velopack package...

:: Copy TrackTurnDatabase
if exist "%~dp0src\iRacingOverlay.Core\Data\TrackTurnDatabase.json" (
    if not exist "%~dp0build\publish\Data" mkdir "%~dp0build\publish\Data"
    copy "%~dp0src\iRacingOverlay.Core\Data\TrackTurnDatabase.json" "%~dp0build\publish\Data\" >nul
    echo Copied TrackTurnDatabase.json
)

:: Get version from VersionInfo.cs (extract major.minor.patch)
:: Velopack uses SemVer: X.Y.Z (not zero-padded)
set PACK_VERSION=0.1.0
echo Packing version: %PACK_VERSION%

:: Pack with Velopack (vpk CLI)
:: Install: dotnet tool install -g vpk
echo.
echo Running vpk pack...
vpk pack ^
    --packId "MRT.MRTui" ^
    --packVersion %PACK_VERSION% ^
    --packDir "%~dp0build\publish" ^
    --mainExe "iRacingOverlay.WPF.exe" ^
    --outputDir "%~dp0build\release"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ============================================
    echo   Velopack packaging failed!
    echo   Make sure vpk is installed:
    echo     dotnet tool install -g vpk
    echo ============================================
    echo.
    echo Falling back to raw publish output...
    xcopy /E /I /Y "%~dp0build\publish" "%~dp0build\release"
    pause
    exit /b 1
)

echo.
echo ============================================
echo   Build + Package complete!
echo   Output: build\release\
echo   Files:
echo     MRT.MRTui-win-Setup.exe  (installer)
echo     MRT.MRTui-%PACK_VERSION%-full.nupkg
echo     RELEASES  (feed index)
echo ============================================
echo.
echo Upload the files in build\release\ to
echo a GitHub Release at github.com/mrt9tv/MRTui
echo.
pause
