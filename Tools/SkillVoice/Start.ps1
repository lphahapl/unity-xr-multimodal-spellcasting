param([int]$Device = -1, [int]$SampleRate = 16000, [string]$Config, [string]$Keywords, [string]$OwnerToken)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$python = Join-Path $root '.venv/Scripts/python.exe'
$script = Join-Path $root 'skill_voice.py'
if (-not (Test-Path -LiteralPath $python)) { throw '请先运行 Setup.ps1 安装依赖和模型。' }
$existing = Get-CimInstance Win32_Process -Filter "Name='python.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($script) }
if ($existing) { throw "语音服务已有进程：$($existing.ProcessId -join ', ')；请先运行 Stop.ps1。" }
if ($Device -lt 0) {
    $selectionText = & $python -X utf8 (Join-Path $root 'select_input.py')
    if ($LASTEXITCODE -ne 0) { throw 'Steam 麦克风尚未就绪，连接后重试。' }
    $selection = $selectionText | ConvertFrom-Json
    $Device = [int]$selection.device
    if (-not $PSBoundParameters.ContainsKey('SampleRate')) { $SampleRate = [int]$selection.rate }
}
$logs = Join-Path $root 'logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$previousLog = Join-Path $logs 'service.jsonl'
if (Test-Path -LiteralPath $previousLog) {
    Copy-Item -LiteralPath $previousLog -Destination (Join-Path $logs ('service-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.jsonl'))
}
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
[IO.File]::WriteAllText((Join-Path $root 'service.owner'), [string]$OwnerToken)
Start-Sleep -Milliseconds 800
$process.Refresh()
if ($process.HasExited) { Get-Content -LiteralPath (Join-Path $logs 'service.jsonl'); throw '语音服务启动失败，请检查日志。' }
Write-Host "语音进程已启动 PID=$($process.Id)。是否就绪以 Unity 状态/日志中的 ready 为准。"
