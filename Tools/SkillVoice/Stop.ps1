$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'skill_voice.py'
$pidFile = Join-Path $PSScriptRoot 'service.pid'
if (-not (Test-Path -LiteralPath $pidFile)) { Write-Host '没有本工具保存的进程号。'; exit 0 }
$serviceId = [int](Get-Content -LiteralPath $pidFile)
$process = Get-CimInstance Win32_Process -Filter "ProcessId=$serviceId"
if ($process -and $process.Name -eq 'python.exe' -and $process.CommandLine.Contains($script)) {
    # A Windows venv redirector can have a second python.exe child.
    Get-CimInstance Win32_Process -Filter "ParentProcessId=$serviceId" | Where-Object {
        $_.Name -eq 'python.exe' -and $_.CommandLine -and $_.CommandLine.Contains($script)
    } | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }
    Stop-Process -Id $serviceId -ErrorAction SilentlyContinue
    Write-Host "已停止语音进程 $serviceId。Unity 将在心跳超时后显示未启动。"
} elseif ($process) { throw '进程号已被其他程序复用，未停止。' }
Remove-Item -LiteralPath $pidFile
