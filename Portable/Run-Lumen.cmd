@echo off
setlocal
pushd "%~dp0"
if not exist "Lumen.exe" (
  call "..\Build-Lumen.cmd"
  if errorlevel 1 exit /b 1
)
start "" "%~dp0Lumen.exe" %*
popd
exit /b 0
