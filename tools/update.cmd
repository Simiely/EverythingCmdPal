@echo off
setlocal
set "ROOT=%~dp0.."
set "DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
set "MAKEAPPX=%ROOT%\tools\buildtools\bin\10.0.26100.0\x64\makeappx.exe"
set "SIGNTOOL=%ROOT%\tools\buildtools\bin\10.0.26100.0\x64\signtool.exe"
set "MSIX=%ROOT%\package\EverythingCmdPal.msix"
set "LAYOUT=%ROOT%\package\layout"
set "SRC=%ROOT%\src\EverythingCmdPal\bin\Debug\net10.0-windows10.0.26100.0\win-x64"

echo [1/7] Build
"%DOTNET%" build "%ROOT%\src\EverythingCmdPal\EverythingCmdPal.csproj" -c Debug -v q
if errorlevel 1 goto fail

echo [2/7] Close Command Palette and extension
powershell -NoProfile -Command "Get-Process Microsoft.CmdPal.UI,EverythingCmdPal -ErrorAction SilentlyContinue | Stop-Process -Force"
timeout /t 2 /nobreak >nul

echo [3/7] Remove old package
powershell -NoProfile -Command "$p = Get-AppxPackage EverythingCmdPal; if ($p) { Remove-AppxPackage -Package $p.PackageFullName }"

echo [4/7] Stage layout
if not exist "%LAYOUT%" mkdir "%LAYOUT%"
xcopy "%SRC%\*" "%LAYOUT%\" /E /Y /Q >nul
copy /Y "%ROOT%\src\EverythingCmdPal\Package.appxmanifest" "%LAYOUT%\AppxManifest.xml" >nul

echo [5/7] Pack
if exist "%MSIX%" del "%MSIX%"
"%MAKEAPPX%" pack /d "%LAYOUT%" /p "%MSIX%" /o >nul
if errorlevel 1 goto fail

echo [6/7] Sign and install
powershell -NoProfile -Command "$c = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=EverythingCmdPal Dev' } | Select-Object -First 1; & '%SIGNTOOL%' sign /fd SHA256 /sha1 $c.Thumbprint '%MSIX%' | Out-Null; Add-AppxPackage -Path '%MSIX%'"
if errorlevel 1 goto fail

echo [7/7] Start Command Palette
explorer "shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App"

echo.
echo Update complete.
goto end

:fail
echo.
echo FAILED. See messages above.

:end
pause
