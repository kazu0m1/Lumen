@echo off
setlocal EnableExtensions EnableDelayedExpansion
pushd "%~dp0"

rem Lumen v1.0.4 portable first-run builder.
rem Prefer the .NET Framework compiler already present on Windows.

set "FRAME="
for %%D in ("%WINDIR%\Microsoft.NET\Framework64\v4.0.30319" "%WINDIR%\Microsoft.NET\Framework\v4.0.30319") do (
  if not defined FRAME if exist "%%~D\csc.exe" set "FRAME=%%~D"
)

if not defined FRAME (
  echo [ERROR] .NET Framework C# compiler csc.exe was not found.
  echo Checked:
  echo   %WINDIR%\Microsoft.NET\Framework64\v4.0.30319
  echo   %WINDIR%\Microsoft.NET\Framework\v4.0.30319
  echo.
  echo Lumen requires the Windows .NET Framework 4.x components.
  if not defined CI pause
  popd
  exit /b 1
)

set "CSC=%FRAME%\csc.exe"
set "PC="
set "PF="
set "WB="
set "SX="

rem 1) Developer/targeting-pack reference assemblies, when installed.
for %%D in (
  "%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1"
  "%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8"
  "%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2"
) do (
  if not defined PF if exist "%%~D\PresentationCore.dll" if exist "%%~D\PresentationFramework.dll" if exist "%%~D\WindowsBase.dll" (
    set "PC=%%~D\PresentationCore.dll"
    set "PF=%%~D\PresentationFramework.dll"
    set "WB=%%~D\WindowsBase.dll"
  )
  if not defined SX if exist "%%~D\System.Xaml.dll" set "SX=%%~D\System.Xaml.dll"
)

rem System.Xaml is a separate WPF/XAML dependency and commonly lives at the Framework root.
if not defined SX if exist "%FRAME%\System.Xaml.dll" set "SX=%FRAME%\System.Xaml.dll"

rem 2) Runtime WPF folders. These are common on Windows 10/11 even when a Developer Pack is absent.
for %%D in (
  "%FRAME%\WPF"
  "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF"
  "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\WPF"
) do (
  if not defined PF if exist "%%~D\PresentationCore.dll" if exist "%%~D\PresentationFramework.dll" if exist "%%~D\WindowsBase.dll" (
    set "PC=%%~D\PresentationCore.dll"
    set "PF=%%~D\PresentationFramework.dll"
    set "WB=%%~D\WindowsBase.dll"
  )
)

rem WindowsBase may also live at the Framework root.
if not defined WB if exist "%FRAME%\WindowsBase.dll" set "WB=%FRAME%\WindowsBase.dll"

rem 3) Standard .NET Framework GAC locations.
set "GAC=%WINDIR%\Microsoft.NET\assembly"
if not defined PC if exist "%GAC%\GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll" set "PC=%GAC%\GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll"
if not defined PC if exist "%GAC%\GAC_MSIL\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll" set "PC=%GAC%\GAC_MSIL\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll"
if not defined PF if exist "%GAC%\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll" set "PF=%GAC%\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll"
if not defined WB if exist "%GAC%\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll" set "WB=%GAC%\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll"
if not defined SX if exist "%GAC%\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll" set "SX=%GAC%\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"

rem 4) Last-resort discovery. Avoid assuming one exact servicing layout.
if not defined PC for /f "delims=" %%F in ('where /r "%WINDIR%\Microsoft.NET" PresentationCore.dll 2^>nul') do if not defined PC set "PC=%%F"
if not defined PF for /f "delims=" %%F in ('where /r "%WINDIR%\Microsoft.NET" PresentationFramework.dll 2^>nul') do if not defined PF set "PF=%%F"
if not defined WB for /f "delims=" %%F in ('where /r "%WINDIR%\Microsoft.NET" WindowsBase.dll 2^>nul') do if not defined WB set "WB=%%F"
if not defined SX for /f "delims=" %%F in ('where /r "%WINDIR%\Microsoft.NET" System.Xaml.dll 2^>nul') do if not defined SX set "SX=%%F"

if not defined PC goto :missingwpf
if not defined PF goto :missingwpf
if not defined WB goto :missingwpf
if not defined SX goto :missingwpf
if not exist "%PC%" goto :missingwpf
if not exist "%PF%" goto :missingwpf
if not exist "%WB%" goto :missingwpf
if not exist "%SX%" goto :missingwpf

if not exist "Portable" mkdir "Portable"
if not exist "Portable\Data" mkdir "Portable\Data"

echo Building Lumen v1.0.4...
echo   Compiler: %CSC%
echo   PresentationCore: %PC%
echo   PresentationFramework: %PF%
echo   WindowsBase: %WB%
echo   System.Xaml: %SX%
echo.

"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /debug:pdbonly ^
 /out:"Portable\Lumen.exe" ^
 /win32icon:"Assets\Lumen.ico" ^
 /win32manifest:"Assets\Lumen.exe.manifest" ^
 /reference:"%FRAME%\System.dll" ^
 /reference:"%FRAME%\System.Core.dll" ^
 /reference:"%FRAME%\System.Drawing.dll" ^
 /reference:"%FRAME%\System.Windows.Forms.dll" ^
 /reference:"%FRAME%\Microsoft.VisualBasic.dll" ^
 /reference:"%WB%" ^
 /reference:"%SX%" ^
 /reference:"%PC%" ^
 /reference:"%PF%" ^
 "Source\Program.cs" "Source\Models.cs" "Source\Settings.cs" "Source\ShellHelpers.cs" "Source\Imaging.cs" "Source\ExifSettingsWindow.cs" "Source\ExportWindow.cs" "Source\MainWindow.cs"

if errorlevel 1 (
  echo.
  echo [ERROR] Build failed. See compiler messages above.
  echo The source tree has not been modified and no photo files were touched.
  if not defined CI pause
  popd
  exit /b 1
)

echo [OK] Portable\Lumen.exe
popd
exit /b 0

:missingwpf
echo [ERROR] One or more WPF assemblies could not be located.
echo.
echo Detected compiler:
echo   %CSC%
echo.
echo Detected WPF references:
if defined PC (echo   PresentationCore: %PC%) else (echo   PresentationCore: NOT FOUND)
if defined PF (echo   PresentationFramework: %PF%) else (echo   PresentationFramework: NOT FOUND)
if defined WB (echo   WindowsBase: %WB%) else (echo   WindowsBase: NOT FOUND)
if defined SX (echo   System.Xaml: %SX%) else (echo   System.Xaml: NOT FOUND)
echo.
echo Lumen v1.0.4 searches the .NET Framework reference assemblies,
echo Framework WPF folders, the GAC, and finally the Microsoft.NET tree.
echo If a component is still shown as NOT FOUND, capture this window and send it back for diagnosis.
if not defined CI pause
popd
exit /b 1
