[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Sample','Capture')][string]$Mode,
    [Parameter(Mandatory)][string]$Output,
    [string]$Candidate,
    [string]$StoneObj,
    [string]$Witness,
    [string]$Frames = '0,21,43,65,86',
    [string]$UnityExe = 'D:/unity_hub/6000.3.10f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$destination = [IO.Path]::GetFullPath($Output)
$paths = @('Assets/Art/Enemies/CanJiOu/CanJiOu_LOD0.fbx',
    'Assets/Art/Enemies/CanJiOu/Animations/CanJiOu_WalkCarryingPerson.fbx',
    'Assets/Art/Enemies/CanJiOu/CanJiOu.mat', 'Assets/Art/Enemies/CanJiOu/Textures/CanJiOu_BaseColor.png',
    'Assets/Materials/Chapter01/CourtyardStone.png')
if ($StoneObj) {
    $paths += @('Assets/Art/Environment/SuspendedPath/SuspendedPathGeometry.asset',
        'Assets/Art/Environment/Opening/TianyiPavilionKit/TimberColor.png','Assets/Materials/Chapter01/WorkshopStoneWall.png')
}
if ($Mode -eq 'Capture') {
    if (-not $Candidate) { throw 'Capture 需要逐帧姿态 CSV' }
    $poseCsv = (Resolve-Path -LiteralPath $Candidate).Path
}
$editor = (Resolve-Path -LiteralPath $UnityExe).Path
$project = Join-Path ([IO.Path]::GetTempPath()) ('Chapter02CarryPose-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($destination)
foreach ($sub in @('Assets/Editor','Packages','ProjectSettings')) {
    [void][IO.Directory]::CreateDirectory((Join-Path $project $sub))
}
foreach ($rel in $paths) {
    $target = Join-Path $project $rel
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    Copy-Item -LiteralPath (Join-Path $root $rel) -Destination $target
    Copy-Item -LiteralPath (Join-Path $root ($rel+'.meta')) -Destination ($target+'.meta')
}
Copy-Item -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $project 'ProjectSettings')
$scripts = @('Chapter02CarryPoseSampler.cs','Chapter02CarryPoseCapture.cs','Chapter02CarryPoseObj.cs')
foreach ($name in $scripts) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $project 'Assets/Editor') }
$packages = '{"dependencies":{"com.unity.modules.animation":"1.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.physics":"1.0.0","com.unity.modules.imgui":"1.0.0"}}'
[IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'),$packages)
$arguments = @('-projectPath',$project,'-batchmode','-noUpm','-poseOut',$destination,
    '-logFile',(Join-Path $destination 'unity-reproduction.log'))
if ($Mode -eq 'Sample') {
    $arguments += @('-nographics','-executeMethod','Chapter02CarryPoseSampler.Run')
} else {
    $arguments += @('-force-d3d11','-executeMethod','Chapter02CarryPoseSampler.Capture','-candidate',$poseCsv,'-poseFrames',$Frames)
    if ($StoneObj) { $arguments += @('-stoneObj',(Resolve-Path -LiteralPath $StoneObj).Path) }
    if ($Witness) { $arguments += @('-witness',(Resolve-Path -LiteralPath $Witness).Path) }
}
$info = [Diagnostics.ProcessStartInfo]::new($editor)
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
foreach ($argument in $arguments) { [void]$info.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($info)
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw ('Unity 验证失败，详见 ' + (Join-Path $destination 'unity-reproduction.log')) }
Write-Output ('Unity 输出 ' + $destination)
