param([Parameter(Mandatory=$true)][string]$Report,
      [string]$Replica="D:/Temp/NarutoVR-CarrierQA-20261125",
      [string]$Editor="D:/unity_hub/6000.3.10f1/Editor/Unity.exe")
$ErrorActionPreference="Stop"
$Replica=(Resolve-Path -LiteralPath $Replica).Path
if ((Split-Path -Leaf $Replica) -ne 'NarutoVR-CarrierQA-20261125') {throw "要求专用独立副本"}
$active=Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($Replica)
}
if ($active) {throw "独立副本正在运行"}
if (Test-Path -LiteralPath $Report) {throw "报告目录已存在，请另命名"}
$dir=(New-Item -Path $Report -ItemType Directory -Force).FullName
$env:QUEST_PATROL_REPORT=$dir
$paths=@('Assets/Scene/Chapter02_SuspendedPath.unity','Assets/scripts/logic/Rpg/CombatRoomEncounter.Spawning.cs',
    'Assets/scripts/logic/Enemies/StoneCarrier.cs','Assets/scripts/logic/Enemies/EnemyController.cs',
    'Assets/scripts/logic/Enemies/EnemyFireCore.cs','Assets/scripts/logic/Enemies/StoneCarrierPost.cs',
    'Assets/ScriptableObjects/敌人战斗配置/StoneCarrierProfile.asset','Assets/ScriptableObjects/敌人战斗配置/A4镇石搬运持姿.asset',
    'Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_BackCarryWalk.fbx','Assets/Art/Enemies/CanJiOu/Animations/CanJiOu.controller',
    'Assets/Art/Enemies/CanJiOu/CanJiOu_Room.prefab','Assets/Art/Enemies/ZhiHuoCanJi/ZhiHuoCanJi_Enemy.prefab',
    'Assets/Art/Enemies/CanJiOu/BallastStone.prefab','Assets/Art/Environment/SuspendedPath/Navigation/Chapter02AltarStoneCarrier_NavMesh.asset',
    'Assets/Editor/Chapter02AltarPreviewWitness.cs','Assets/Editor/QuestActorGeometry.cs','Assets/Editor/QuestPatrolPaceCheck.cs')
foreach($p in 'Assets/scripts/logic/Enemies/Spawning/EnemyLease.cs','Assets/scripts/logic/Enemies/Spawning/EncounterEnemyPace.cs','Assets/scripts/logic/Enemies/Spawning/EncounterEnemyPace.cs.meta'){
    if(Test-Path -LiteralPath (Join-Path $Replica $p)){$paths+=$p}
}
foreach($d in (Get-ChildItem -LiteralPath (Join-Path $Replica 'Assets/ScriptableObjects/敌人定义配置') -File -Filter '*.asset')){
    $paths+="Assets/ScriptableObjects/敌人定义配置/$($d.Name)"
}
$records=foreach($p in $paths) {
    [ordered]@{path=$p; replica=(Get-FileHash -LiteralPath (Join-Path $Replica $p)).Hash}
}
$records | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $dir 'inputs.json')
$argsUnity=@('-batchmode','-force-d3d11','-projectPath',('"{0}"' -f $Replica),'-executeMethod','QuestPatrolPaceCheck.Run',
    '-logFile',('"{0}"' -f (Join-Path $dir 'play.log')),'-job-worker-count','4')
$xrFile=Join-Path $Replica 'Assets/XR/XRGeneralSettingsPerBuildTarget.asset'
$xrBytes=[IO.File]::ReadAllBytes($xrFile)
try {
    $xrText=[Text.Encoding]::UTF8.GetString($xrBytes)
    if($xrText -notmatch 'm_InitManagerOnStart: [01]'){throw "XR 设置格式不匹配"}
    [IO.File]::WriteAllText($xrFile, $xrText.Replace('m_InitManagerOnStart: 1','m_InitManagerOnStart: 0'), [Text.UTF8Encoding]::new($false))
    $env:XR_RUNTIME_JSON=Join-Path $dir 'no-hardware-runtime.json'
    $process=Start-Process -FilePath $Editor -ArgumentList $argsUnity -WindowStyle Hidden -Wait -PassThru
} finally {
    [IO.File]::WriteAllBytes($xrFile,$xrBytes)
}
Set-Content -LiteralPath (Join-Path $dir 'exit.txt') -Value $process.ExitCode
exit $process.ExitCode
