param([Parameter(Mandatory = $true)][string]$SdkRoot)

$ErrorActionPreference = 'Stop'
$adbPath = (Resolve-Path -LiteralPath (Join-Path $SdkRoot 'platform-tools/adb.exe')).Path
# Meta's native editor tools can fail to convert a non-ASCII ADB executable path.
# Check the daemon, because setting ANDROID_HOME does not replace an existing one.
$previousPreference = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $serverStatus = (& $adbPath server-status 2>$null | Out-String)
    $statusCode = $LASTEXITCODE
} finally {
    $ErrorActionPreference = $previousPreference
}
$serverPath = $null
if ($serverStatus -match '(?m)^executable_absolute_path:\s*(.+)$') {
    $serverPath = ConvertFrom-Json $Matches[1]
}
if ($statusCode -ne 0 -or $serverPath -ne $adbPath) {
    Write-Host 'Switching to Unity SDK ADB. USB debugging connections may briefly reconnect.'
    & $adbPath kill-server
    if ($LASTEXITCODE -ne 0) { throw 'Could not stop the previous ADB server.' }
}
& $adbPath start-server
if ($LASTEXITCODE -ne 0) { throw 'Could not start Unity SDK ADB.' }
$serverStatus = (& $adbPath server-status | Out-String)
if ($serverStatus -notmatch '(?m)^executable_absolute_path:\s*(.+)$' -or
    (ConvertFrom-Json $Matches[1]) -ne $adbPath) {
    throw 'Another application replaced the ADB server. Close its phone debugging tools and retry.'
}
Write-Host "ADB ready: $adbPath"
