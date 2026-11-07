param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('debug-text-comparison','storage-timeout-check','architecture-interface-check','score-ui-probe','scored-throw-check','configure-scored-throw','wrist-roots','wrist-lifecycle','throw-ease-probe','wrist-tap','wrist-tap-check','wrist-ray-probe','wrist-ease','configure-wrist','wrist-check','wrist-preview','feel-check','configure-feel','configure-navigation','navigation-check','record-throws','clock-check','refresh','pose-regression','configure-hand-poses','configure-interactions','interaction-check','play-user','full-editor-check','play-check','stop','preview','state','final-state','console','clear-console','open-rpg','open-shield-trial','open-p1','p1-check')]
    [string]$Action
)
$ErrorActionPreference = 'Stop'
$taskProject = Split-Path -Parent $PSScriptRoot
$taskLogs = Join-Path $taskProject 'Logs'
New-Item -ItemType Directory -Path $taskLogs -Force | Out-Null
$taskRequest = Join-Path $taskLogs 'linear-rpg.request'
if (Test-Path -LiteralPath $taskRequest) { throw 'A previous RPG request is still pending. Wait for Unity to consume it.' }
$taskPending = Join-Path $taskLogs ('rpg-request-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    [IO.File]::WriteAllText($taskPending, $Action, [Text.Encoding]::ASCII)
    Move-Item -LiteralPath $taskPending -Destination $taskRequest
} finally {
    if (Test-Path -LiteralPath $taskPending) { Remove-Item -LiteralPath $taskPending }
}
