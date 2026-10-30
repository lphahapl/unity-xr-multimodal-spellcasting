@echo off
powershell.exe -NoProfile -File "%~dp0Tools\Start-Unity.ps1" %*
if errorlevel 1 pause
