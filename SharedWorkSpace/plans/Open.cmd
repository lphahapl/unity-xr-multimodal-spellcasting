@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\start-workspace.ps1" -Route plans/
if errorlevel 1 pause

