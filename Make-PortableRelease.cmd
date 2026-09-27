@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "CI=1"
call Build-Lumen.cmd
if errorlevel 1 exit /b 1

set "VERSION=1.0.4"
set "DIST=%~dp0dist"
set "STAGE=%DIST%\Lumen-v%VERSION%-win-portable"
set "ZIP=%DIST%\Lumen-v%VERSION%-win-portable.zip"

if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%ZIP%" del /q "%ZIP%"
mkdir "%STAGE%"
mkdir "%STAGE%\Data"

copy /y "Portable\Lumen.exe" "%STAGE%\Lumen.exe" >nul
copy /y "Portable\Lumen.exe.config" "%STAGE%\Lumen.exe.config" >nul
copy /y "LICENSE.txt" "%STAGE%\LICENSE.txt" >nul
copy /y "README.md" "%STAGE%\README.md" >nul
copy /y "README.ja.md" "%STAGE%\README.ja.md" >nul

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "Compress-Archive -Path '%STAGE%\*' -DestinationPath '%ZIP%' -CompressionLevel Optimal -Force"
if errorlevel 1 (
  echo.
  echo ERROR: Could not create release ZIP.
  exit /b 1
)

for /f "tokens=*" %%H in ('powershell.exe -NoProfile -Command "(Get-FileHash -Algorithm SHA256 '%ZIP%').Hash.ToLowerInvariant()"') do set "HASH=%%H"

echo.
echo Created:
echo   %ZIP%
echo SHA-256:
echo   %HASH%
echo.
exit /b 0
