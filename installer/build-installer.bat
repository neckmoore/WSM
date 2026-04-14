@echo off
setlocal EnableDelayedExpansion
title WSM Monitor — build installer
cd /d "%~dp0"
set "ROOT=%~dp0.."
set "STAGING=%~dp0staging"
set "SRCREV="
set "ISCC="

REM ISCC must exist before publish. Check common paths (per-user Inno is often NOT on PATH).
if defined INNO_SETUP (
  if exist "!INNO_SETUP!\ISCC.exe" set "ISCC=!INNO_SETUP!\ISCC.exe"
)
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC (
  for /f "usebackq delims=" %%I in (`where ISCC.exe 2^>nul`) do (
    set "ISCC=%%I"
    goto :_inno_ok
  )
)
:_inno_ok
if not defined ISCC (
  echo.
  echo ========================================================================
  echo   Inno Setup 6 not found — WSMMonitor-Setup-*.exe will NOT be built.
  echo   staging\ is only input for Inno; it is not the installer.
  echo.
  echo   Install: https://jrsoftware.org/isinfo.php
  echo   Or set INNO_SETUP to the folder that contains ISCC.exe.
  echo ========================================================================
  echo.
  pause
  exit /b 1
)

echo Using ISCC: !ISCC!
echo.

for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')"`) do set "SRCREV=%%I"
if not defined SRCREV (
  echo Failed to compute UTC build stamp.
  pause
  exit /b 1
)

echo Staging folder: !STAGING!
if exist "!STAGING!" rmdir /s /q "!STAGING!"

echo Publishing self-contained win-x64...
dotnet publish "!ROOT!\WSMMonitor.App\WSMMonitor.App.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=false ^
  -p:IncludeNativeLibrariesForSelfExtract=false ^
  -p:SourceRevisionId=!SRCREV! ^
  -o "!STAGING!"
if errorlevel 1 (
  echo dotnet publish failed.
  pause
  exit /b 1
)

echo.
echo Compiling installer...
"!ISCC!" "%~dp0WSMMonitor.iss"
if errorlevel 1 (
  echo ISCC failed. Fix errors above.
  pause
  exit /b 1
)

echo.
echo Done. Installer:
dir /b "%~dp0Output\WSMMonitor-Setup-*.exe" 2>nul
if errorlevel 1 echo Check folder: %~dp0Output
echo.
pause
endlocal
exit /b 0
