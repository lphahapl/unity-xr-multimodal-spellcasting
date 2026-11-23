@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\start-workspace.ps1" -Route reader/
if errorlevel 1 pause

