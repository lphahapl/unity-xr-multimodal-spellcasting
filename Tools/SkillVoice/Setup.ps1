param([string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $root '.venv/Scripts/python.exe'))) {
    & $Python -m venv (Join-Path $root '.venv')
    if ($LASTEXITCODE -ne 0) { throw 'Python venv creation failed' }
}
$localPython = Join-Path $root '.venv/Scripts/python.exe'
& $localPython -m pip install --disable-pip-version-check -r (Join-Path $root 'requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed' }
& $localPython -X utf8 (Join-Path $root 'setup_model.py')
if ($LASTEXITCODE -ne 0) { throw 'Official model download failed' }
& $localPython -X utf8 (Join-Path $root 'skill_voice.py') --self-test
if ($LASTEXITCODE -ne 0) { throw 'Local self test failed' }
