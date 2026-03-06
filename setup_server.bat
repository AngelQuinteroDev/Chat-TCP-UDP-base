@echo off
chcp 65001 >nul
echo.
echo Ejecutando setup_server.ps1...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup_server.ps1"
