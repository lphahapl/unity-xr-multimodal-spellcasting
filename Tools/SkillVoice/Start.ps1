param([int]$Device = -1, [int]$SampleRate = 16000, [string]$Config, [string]$Keywords)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$python = Join-Path $root '.venv/Scripts/python.exe'
$script = Join-Path $root 'skill_voice.py'
if (-not (Test-Path -LiteralPath $python)) { throw '请先运行 Setup.ps1 安装依赖和模型。' }
$existing = Get-CimInstance Win32_Process -Filter "Name='python.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($script) }
if ($existing) { throw "语音服务已有进程：$($existing.ProcessId -join ', ')；请先运行 Stop.ps1。" }
$logs = Join-Path $root 'logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$arguments = @('-X', 'utf8', '-u', ('"' + $script + '"'), '--sample-rate', $SampleRate)
if ($Device -ge 0) { $arguments += @('--device', $Device) }
foreach ($entry in @(@('--config', $Config), @('--keywords', $Keywords))) {
    if (-not [string]::IsNullOrWhiteSpace($entry[1])) {
        $resolvedInput = (Resolve-Path -LiteralPath $entry[1] -ErrorAction Stop).Path
        $arguments += @($entry[0], ('"' + $resolvedInput + '"'))
    }
}
$process = Start-Process -FilePath $python -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'service.jsonl') -RedirectStandardError (Join-Path $logs 'service.stderr.log')
$process.Id | Set-Content -LiteralPath (Join-Path $root 'service.pid')
Start-Sleep -Milliseconds 800
$process.Refresh()
if ($process.HasExited) { Get-Content -LiteralPath (Join-Path $logs 'service.jsonl'); throw '语音服务启动失败，请检查日志。' }
Write-Host "语音进程已启动 PID=$($process.Id)。是否就绪以 Unity 状态/日志中的 ready 为准。"
