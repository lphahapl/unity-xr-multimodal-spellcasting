param(
    [Parameter(Mandatory=$true)][string]$Replica,
    [Parameter(Mandatory=$true)][string]$Report,
    [string]$Editor="D:/unity_hub/6000.3.10f1/Editor/Unity.exe"
)
$ErrorActionPreference="Stop"
$source=(Resolve-Path "$PSScriptRoot/../../..").Path.TrimEnd('\')
$project=(Resolve-Path -LiteralPath $Replica).Path.TrimEnd('\')
$target=[IO.Path]::GetFullPath($Report)
function IsWithin($path,$parent) {
    return $path.Equals($parent,[StringComparison]::OrdinalIgnoreCase) -or $path.StartsWith($parent+'\',[StringComparison]::OrdinalIgnoreCase)
}
if((IsWithin $project $source) -or (IsWithin $source $project) -or (IsWithin $target $source) -or (IsWithin $target $project)){
    throw "请选择独立的工程副本及工程外的报告目录。"
}
# Only write new QA files into ordinary clone directories. Refuse linked directories and unrelated name collisions.
$assetDir=Join-Path $project 'Assets/scripts/logic/Editor'
foreach($dir in @($assetDir,(Split-Path $assetDir),(Split-Path (Split-Path $assetDir)),(Join-Path $project 'Assets'),$project)){
    if((Get-Item -LiteralPath $dir).Attributes -band [IO.FileAttributes]::ReparsePoint){throw "副本目录为链接：$dir"}
}
$xr=Get-Content -Raw -LiteralPath "$project/Assets/XR/XRGeneralSettingsPerBuildTarget.asset"
if($xr -match 'm_InitManagerOnStart: 1|m_AutomaticLoading: 1|m_AutomaticRunning: 1' -or $xr -notmatch 'm_InitManagerOnStart: 0' -or
    $xr -match '(?m)^  m_Loaders:(?! \[\]\s*$)'){throw "先在独立副本内关闭所有 XR 自动初始化，并将各平台 m_Loaders 清空。"}
if(Test-Path "$project/Temp/UnityLockfile"){throw "请使用已关闭的副本。"}
if(Test-Path -LiteralPath $target){throw "请选择新的报告目录。"}
foreach($name in 'ChapterWalkCheck.cs','ChapterWalkRoutes.cs'){
    $dest=Join-Path $assetDir $name
    $src=Join-Path $PSScriptRoot $name
    if(!(Test-Path -LiteralPath $dest)){[IO.File]::Copy($src,$dest,$false)}
    elseif((Get-FileHash -LiteralPath $dest).Hash -ne (Get-FileHash -LiteralPath $src).Hash){throw "副本存在不同的 $name"}
}
[IO.Directory]::CreateDirectory($target) | Out-Null
$env:CH02_WALK_PROJECT=$project
$env:CH02_WALK_SOURCE=$source
$env:CH02_WALK_REPORT=$target
$env:CH02_WALK_MODE="routes"
$env:ALLUSERSPROFILE=$env:ProgramData
$env:XR_RUNTIME_JSON=Join-Path $target 'no-hardware-runtime.json'
$argsUnity=@('-batchmode','-nographics','-job-worker-count','3','-projectPath',('"'+$project+'"'),'-logFile',('"'+$target+'\play.log"'),'-executeMethod','ChapterWalkCheck.Run')
$process=Start-Process -FilePath $Editor -ArgumentList $argsUnity -WindowStyle Hidden -Wait -PassThru
[IO.File]::WriteAllText((Join-Path $target 'exit.txt'),[string]$process.ExitCode)
exit $process.ExitCode