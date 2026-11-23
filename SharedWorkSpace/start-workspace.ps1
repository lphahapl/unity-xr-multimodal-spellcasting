param([ValidateSet('', 'rig/', 'plans/', 'scenes/', 'reader/')][string]$Route = '')
$ErrorActionPreference = 'Stop'
$port = if ($env:RIG_WORKBENCH_PORT) { [int]$env:RIG_WORKBENCH_PORT } else { 4190 }
$address = "http://127.0.0.1:$port"
function Get-Workspace {
  try { Invoke-RestMethod -Uri "$address/api/identity" -TimeoutSec 2 } catch { $null }
}
$instance = Get-Workspace
if (!$instance) {
  $nodePath = (Get-Command node.exe -ErrorAction Stop).Source
  $service = Start-Process -FilePath $nodePath -ArgumentList 'server.mjs' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
  for ($tryNumber = 0; $tryNumber -lt 35; $tryNumber++) {
    Start-Sleep -Milliseconds 150
    $instance = Get-Workspace
    if ($instance -or $service.HasExited) { break }
  }
}
if (!$instance -or $instance.root -ne (Resolve-Path -LiteralPath $PSScriptRoot).Path) {
  throw "Port $port is occupied or this workspace could not start. Run node server.mjs in this folder for details."
}
$chrome = @("${env:ProgramFiles}\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe", "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($chrome) { Start-Process -FilePath $chrome -ArgumentList "$address/$Route" }
else { Start-Process "$address/$Route" }

