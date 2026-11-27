$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$root = Join-Path $PSScriptRoot 'fixtures'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $voice = $speaker.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.Name -eq 'zh-CN' } | Select-Object -First 1
    if (-not $voice) { throw '未安装 Windows 中文 TTS。可使用自录 PCM 16-bit WAV 代替。' }
    $speaker.SelectVoice($voice.VoiceInfo.Name)
    $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
    $phrases = [ordered]@{ fireball = '火球术'; cancel = '取消施法'; negative = '今天天气很好，我们出去散步。' }
    foreach ($entry in $phrases.GetEnumerator()) {
        $path = Join-Path $root ($entry.Key + '.wav')
        $speaker.SetOutputToWaveFile($path, $format)
        $speaker.Speak($entry.Value)
        $speaker.SetOutputToNull()
        Write-Host "$($entry.Key): $($entry.Value) -> $path"
    }
    # 衡印样本来自首章 SH01 的实测片段，其来源与切点保存在 SOURCE.json。
    $metadataPath = Join-Path $root 'SOURCE.json'
    $metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding utf8 | ConvertFrom-Json
    foreach ($entry in $phrases.GetEnumerator()) {
        $metadata.phrases.($entry.Key) = $entry.Value
    }
    $metadata.voice = $voice.VoiceInfo.Name
    $metadata.generatedUtc = [DateTime]::UtcNow.ToString('o')
    $metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $metadataPath -Encoding utf8
} finally { $speaker.Dispose() }
