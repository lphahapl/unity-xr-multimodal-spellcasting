param([string]$Path)
$ErrorActionPreference = 'Stop'
if (!$Path) {
    $traceRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'Logs/skill-casts'
    $latestTrace = Get-ChildItem -LiteralPath $traceRoot -Filter '*.jsonl' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (!$latestTrace) { throw '没有施法日志，请先进入 Play。' }
    $Path = $latestTrace.FullName
}
Write-Output "施法链条：$Path"
$rows = @(Get-Content -LiteralPath $Path -Encoding utf8 | ForEach-Object {
    try { $_ | ConvertFrom-Json } catch { Write-Warning '跳过尚未写完或损坏的一行' }
})
$rows | Group-Object stage | Select-Object Name,Count | Format-Table -AutoSize
$rows | Where-Object { $_.stage -notin @('match_snapshot','voice_health','throw_frame','throw_warming_up') } |
    Select-Object sequence,time,stage,skillId,attempt,detail | Format-Table -Wrap
Write-Output '最近五次匹配门槛（骨骼间隔、质量、歧义、保持时间）：'
$rows | Where-Object stage -eq 'match_snapshot' | Select-Object -Last 5 time,detail,gates | Format-List
Write-Output '最近音频设备状态：'
$rows | Where-Object stage -eq 'voice_health' | Select-Object -Last 1 time,detail | Format-List
