@echo off
rem One-click setup/update. Works on a fresh machine:
rem installs .NET SDK, creates+trusts the dev cert (one UAC), builds and installs.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update.ps1"
pause
