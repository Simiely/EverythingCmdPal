@echo off
setlocal
set "CER=%~dp0..\package\dev.cer"

echo.
echo Installing EverythingCmdPal development certificate
echo Certificate file: %CER%
echo.

if not exist "%CER%" goto nofile

certutil -addstore -f Root "%CER%"
if errorlevel 1 goto failed

certutil -addstore -f TrustedPeople "%CER%"
if errorlevel 1 goto failed

echo.
echo Done. Return to the chat and tell the assistant to continue.
goto end

:nofile
echo.
echo ERROR: certificate not found.
goto end

:failed
echo.
echo ERROR: failed. Make sure this window is running as Administrator.

:end
echo.
pause
