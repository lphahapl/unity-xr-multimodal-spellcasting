@echo off
setlocal
pushd "%~dp0"
powershell.exe -NoProfile -Command "$chatLink = Read-Host 'Gemini chat URL'; if ($chatLink) { & node.exe ./gemini-inbox.mjs $chatLink; exit $LASTEXITCODE }"
if errorlevel 1 pause
popd
