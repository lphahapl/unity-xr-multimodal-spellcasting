param([string]$ProjectPath, [switch]$Validate, [switch]$Configure)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = if ($ProjectPath) { (Resolve-Path -LiteralPath $ProjectPath).Path } else { $workspaceRoot }
if ($Validate -and $Configure) { throw 'Choose either Validate or Configure.' }
if (($Validate -or $Configure) -and $projectRoot -ne $workspaceRoot) {
    throw 'Configuration and validation methods belong to NarutoVR only.'
}
$versionLine = Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') |
    Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
$editorVersion = $versionLine -replace '^m_EditorVersion: ', ''
$editorPath = "D:/unity_hub/$editorVersion/Editor/Unity.exe"
if (-not (Test-Path -LiteralPath $editorPath)) {
    throw "Unity editor not found: $editorPath. Update the installation path in this script."
}

$sdkRoot = Join-Path (Split-Path -Parent $editorPath) 'Data/PlaybackEngines/AndroidPlayer/SDK'
& (Join-Path $PSScriptRoot 'Use-UnityAdb.ps1') -SdkRoot $sdkRoot

# Some command runners omit this standard Windows variable. UPM requires it.
# This only changes the child process environment, never Windows system settings.
if (-not $env:ALLUSERSPROFILE) {
    $env:ALLUSERSPROFILE = [Environment]::GetFolderPath('CommonApplicationData')
}
# Keep generated Windows Prefab command paths ASCII for this project only.
# This changes only the launched editor's environment, never Windows user settings.
if ($projectRoot -eq $workspaceRoot) {
    $taskGradleHome = Join-Path $workspaceRoot '.build-cache/gradle'
    New-Item -ItemType Directory -Path $taskGradleHome -Force | Out-Null
    $env:GRADLE_USER_HOME = $taskGradleHome
}
$launchArguments = @('-projectPath', ('"' + $projectRoot + '"'))
if ($Validate -or $Configure) {
    $logName = if ($Configure) { 'quest-setup-current.log' } else { 'quest-cold-start-validation.log' }
    $method = if ($Configure) { 'ConfigureAndValidate' } else { 'Validate' }
    $validationLog = Join-Path $workspaceRoot "Logs/$logName"
    $launchArguments += @('-batchmode', '-force-d3d11', '-executeMethod',
        "NarutoVR.Editor.QuestEnvironmentSetup.$method", '-quit',
        '-logFile', ('"' + $validationLog + '"'))
    $editorProcess = Start-Process $editorPath -ArgumentList $launchArguments -WindowStyle Hidden -PassThru -Wait
    exit $editorProcess.ExitCode
}
$logName = if ($projectRoot -eq $workspaceRoot) { 'quest-editor-ready.log' } else { 'education-editor-ready.log' }
$editorLog = Join-Path $workspaceRoot "Logs/$logName"
$launchArguments += @('-logFile', ('"' + $editorLog + '"'))
Start-Process $editorPath -ArgumentList $launchArguments -WindowStyle Normal
