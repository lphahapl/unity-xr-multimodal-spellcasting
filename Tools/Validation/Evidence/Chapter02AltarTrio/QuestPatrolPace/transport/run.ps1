param([Parameter(Mandatory=$true)][string]$Report)
$ErrorActionPreference='Stop'
$box=(Resolve-Path -LiteralPath 'D:/Temp/NarutoVR-CarrierQA-20261125').Path
$active=Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($box)}
if($active -or (Test-Path -LiteralPath "$box/Temp/UnityLockfile")){throw '独立工程已被占用'}
$sceneHash=(Get-FileHash -LiteralPath "$box/Assets/Scene/Chapter02_SuspendedPath.unity").Hash
if($sceneHash -ne 'E24E6A26CD18E97A2856BF60BAAE08B8BAE3DA1731B2FE748AED6348D55264E3'){throw "场景基线不符 $sceneHash"}
if(Test-Path -LiteralPath $Report){throw '本次报告目录必须尚未使用'}
$dir=(New-Item -ItemType Directory -Path $Report).FullName
$checkpoint=Join-Path $PSScriptRoot 'postrepair.json'
Copy-Item -LiteralPath $checkpoint -Destination "$dir/postrepair.json"
$probe='Assets/scripts/logic/Editor/QuestTransportReentry.cs'
Copy-Item -LiteralPath "$PSScriptRoot/QuestTransportReentry.cs" -Destination "$box/$probe"
$inputs=@('Assets/Scene/Chapter02_SuspendedPath.unity',$probe)
$parts='CombatRoomEncounter*','EnemyController*','EnemyLease','EncounterEnemyPace','StoneCarrier*','Ballast*','Chapter02Basket*','Chapter02BridgeEquipment','RpgLocomotionController*','GlyphPen*','*Qianjun*','LinearRpgController*','*HandDraw*'
$inputs+=@(Get-ChildItem -LiteralPath "$box/Assets/scripts" -Recurse -File -Filter '*.cs' | Where-Object {
    $name=$_.BaseName; ($parts | Where-Object {$name -like $_} | Select-Object -First 1) -ne $null
} | ForEach-Object {$_.FullName.Substring($box.Length+1).Replace('\','/')})
$inputHashes=foreach($item in ($inputs | Select-Object -Unique)){[pscustomobject]@{path=$item;sha256=(Get-FileHash -LiteralPath "$box/$item").Hash}}
$inputHashes | ConvertTo-Json | Set-Content -Encoding utf8 "$dir/inputs.json"
$env:CH02_PACE_TRANSPORT_OUT=$dir
$env:ALLUSERSPROFILE=$env:ProgramData
$env:XR_RUNTIME_JSON=Join-Path $dir 'no-hardware-runtime.json'
$xrFile=Join-Path $box 'Assets/XR/XRGeneralSettingsPerBuildTarget.asset'
$original=[IO.File]::ReadAllBytes($xrFile)
$before=(Get-FileHash -LiteralPath $xrFile).Hash
try {
    $data=[Text.Encoding]::UTF8.GetString($original)
    if($data -notmatch 'm_InitManagerOnStart: [01]'){throw 'XR 配置格式未知'}
    [IO.File]::WriteAllText($xrFile,$data.Replace('m_InitManagerOnStart: 1','m_InitManagerOnStart: 0'),[Text.UTF8Encoding]::new($false))
    $disabled=(Get-FileHash -LiteralPath $xrFile).Hash
    $unity=Start-Process -FilePath 'D:/unity_hub/6000.3.10f1/Editor/Unity.exe' -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"{0}"' -f $box),'-job-worker-count','3','-executeMethod','NarutoVR.SealCombat.Editor.QuestTransportReentry.Run','-logFile',('"{0}"' -f "$dir/play.log")) -WindowStyle Hidden -Wait -PassThru
} finally {
    [IO.File]::WriteAllBytes($xrFile,$original)
    $after=(Get-FileHash -LiteralPath $xrFile).Hash
    [pscustomobject]@{scene=$sceneHash;checkpoint=(Get-FileHash -LiteralPath $checkpoint).Hash;xrBefore=$before;xrDisabled=$disabled;xrRestored=$after;restored=($before -eq $after)} | ConvertTo-Json | Set-Content -Encoding utf8 "$dir/isolation.json"
}
$unity.ExitCode | Set-Content "$dir/exit.txt"
exit $unity.ExitCode
