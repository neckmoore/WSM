@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0WSMMonitor.App"

rem UTC stamp → MSBuild SourceRevisionId → AssemblyInformationalVersion = Version+stamp (see csproj IncludeSourceRevisionInInformationalVersion)
for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "[DateTime]::UtcNow.ToString('yyyyMMddHHmmss')"`) do set "SRCREV=%%I"
if not defined SRCREV (
  echo Failed to compute UTC build stamp.
  exit /b 1
)

echo Building self-contained single-file EXE (.NET 8 SDK)...
echo   SourceRevisionId=!SRCREV! ^(each publish run gets a unique build label^)
echo.

dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:SourceRevisionId=!SRCREV! ^
  -o "%~dp0publish"
if errorlevel 1 exit /b 1

echo.
echo Done: %~dp0publish\WSMMonitor.exe
echo Footer / X-WSM-Version use BuildIdentity (e.g. 1.0.3+!SRCREV!). Restart service from this folder if installed.
endlocal
