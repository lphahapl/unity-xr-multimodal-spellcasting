param(
    [string]$Results = (Join-Path (Get-Location) ('Logs/CH02-AltarTrio-20261126/' + (Get-Date -Format yyyyMMdd-HHmmss))),
    [string[]]$Orders = @('carrier-first', 'carrier-last', 'live'),
    [string]$UseReplicaSceneHash
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$qa = 'D:/Temp/NarutoVR-CarrierQA-20261125'
$unity = 'D:/unity_hub/6000.3.10f1/Editor/Unity.exe'
$patch = Join-Path $PSScriptRoot '../SceneHandoff/Chapter02AltarTrioInstaller.cs'
if (-not (Test-Path -LiteralPath $patch)) { throw '缺少场景负责人的三敌安装器' }
if ($Orders | Where-Object { $_ -notin @('carrier-first','carrier-last','live') }) { throw '不支持的验收项目' }
$out = [IO.Path]::GetFullPath($Results)
New-Item -ItemType Directory -Path $out -Force | Out-Null
Push-Location $root
try {
    if ($UseReplicaSceneHash) {
        $replicaScene = Join-Path $qa 'Assets/Scene/Chapter02_SuspendedPath.unity'
        if ((Get-FileHash -LiteralPath $replicaScene).Hash -ne $UseReplicaSceneHash) { throw '专属副本与指定场景快照不符' }
    } else {
        python Tools/Validation/Evidence/T25Approved/SceneHandoff/prepare_snapshot.py $qa (Join-Path $out 'mirror')
        if ($LASTEXITCODE -ne 0) { throw '生产与副本输入核对失败' }
    }
    $probe = Join-Path $PSScriptRoot 'Chapter02AltarEncounterCheck.cs'
    $liveProbe = Join-Path $PSScriptRoot 'Chapter02AltarLiveCheck.cs'
    $previewProbe = Join-Path $PSScriptRoot 'Chapter02AltarPreviewWitness.cs'
    foreach ($script in @($probe, $patch, $liveProbe, $previewProbe)) {
        $dest = Join-Path $qa ('Assets/Editor/'+[IO.Path]::GetFileName($script))
        Copy-Item -LiteralPath $script -Destination $dest
        if ((Get-FileHash -LiteralPath $script).Hash -ne (Get-FileHash -LiteralPath $dest).Hash) { throw '测试输入不同' }
    }
    $inputs = @('Assets/Scene/Chapter02_SuspendedPath.unity','Assets/scripts/logic/Rpg/CombatRoomEncounter.cs',
        'Assets/scripts/logic/Rpg/CombatRoomEncounter.Spawning.cs','Assets/scripts/logic/Enemies/BallastBasket.cs',
        'Assets/scripts/logic/Enemies/StoneCarrier.cs','Assets/scripts/logic/Enemies/StoneCarrierPost.cs',
        'Assets/scripts/logic/Enemies/Spawning/EnemyManager.cs','Assets/scripts/logic/Enemies/EnemyController.cs',
        'Assets/ScriptableObjects/敌人目录配置/本关怪物目录.asset','Assets/Art/Enemies/CanJiOu/BallastStone.prefab',
        'Assets/Art/Enemies/CanJiOu/CanJiOu_Room.prefab','Assets/Art/Enemies/ZhiHuoCanJi/ZhiHuoCanJi_Enemy.prefab',
        'Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_BackCarryWalk.fbx','Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_BackCarryWalk.fbx.meta',
        'Assets/Art/Enemies/CanJiOu/Animations/CanJiOu.controller','Assets/ScriptableObjects/敌人战斗配置/StoneCarrierProfile.asset',
        'Assets/ScriptableObjects/敌人战斗配置/A4镇石搬运持姿.asset',
        'Assets/Art/Environment/SuspendedPath/Navigation/Chapter02AltarStoneCarrier_NavMesh.asset',
        'Assets/scripts/logic/Rpg/Glyphs/Chapter02BridgeEquipment.cs','Assets/scripts/logic/Rpg/Chapter02Transport/Chapter02BasketMotion.cs')
    $hashes = foreach ($relative in $inputs) {
        $h = (Get-FileHash -LiteralPath (Join-Path $root $relative)).Hash
        $test = (Get-FileHash -LiteralPath (Join-Path $qa $relative)).Hash
        if ($h -ne $test -and !($UseReplicaSceneHash -and $relative -eq 'Assets/Scene/Chapter02_SuspendedPath.unity' -and $test -eq $UseReplicaSceneHash)) { throw "生产与测试输入不同 $relative" }
        [pscustomobject]@{path=$relative; production=$h; replica=$test}
    }
    $hashes | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $out 'tested-inputs.json')
    foreach ($order in $Orders) {
        $dir = Join-Path $out $order
        if (Test-Path -LiteralPath (Join-Path $dir 'assertions.txt')) { throw "该顺序已有证据，请另取输出目录 $dir" }
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        $env:ALTAR_TRIO_ORDER = $order
        $env:ALTAR_TRIO_EVIDENCE = $dir
        $log = Join-Path $dir 'play.log'
        $method = if ($order -eq 'live') { 'Chapter02AltarLiveCheck.Run' } else { 'Chapter02AltarEncounterCheck.Run' }
        $argsList = @('-batchmode','-nographics','-projectPath',$qa,'-executeMethod',$method,'-logFile',('"' + $log + '"'),'-job-worker-count','4')
        $proc = Start-Process -FilePath $unity -ArgumentList $argsList -WindowStyle Hidden -Wait -PassThru
        $proc.ExitCode | Set-Content -LiteralPath (Join-Path $dir 'exit.txt')
        if ($proc.ExitCode -ne 0) { throw "$order 失败，请查阅 $log" }
    }
} finally { Pop-Location }
