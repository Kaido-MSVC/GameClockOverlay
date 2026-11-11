param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (Get-Process -Name GameClockOverlay -ErrorAction SilentlyContinue) { throw 'Close GameClockOverlay before running isolated hotkey / single-instance tests.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root ('artifacts/validation-' + [guid]::NewGuid().ToString('N')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw "Test directory already exists: $OutputDirectory" }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
function Invoke-Check([string]$name, [string[]]$arguments) {
    & dotnet @arguments 2>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory "$name.log")
    if ($LASTEXITCODE -ne 0) { throw "$name failed ($LASTEXITCODE)" }
}
Push-Location $root
try {
    Invoke-Check 'build' @('build','GameClockOverlay.slnx','-c','Release','-p:PlatformTarget=x64')
    Invoke-Check 'main' @('run','--project','tests','-c','Release','--no-build','--',(Join-Path $OutputDirectory 'main'))
    Invoke-Check 'display' @('run','--project','tests','-c','Release','--no-build','--','--display')
    Invoke-Check 'lifecycle' @('run','--project','tests','-c','Release','--no-build','--','--lifecycle',(Join-Path $OutputDirectory 'lifecycle'))
    Invoke-Check 'reload' @('run','--project','tests','-c','Release','--no-build','--','--lifecycle',(Join-Path $OutputDirectory 'lifecycle'),'reload')
    Invoke-Check 'instances' @('run','--project','tests','-c','Release','--no-build','--','--instances')
    Invoke-Check 'fixes' @('run','--project','tests','-c','Release','--no-build','--','--fixes',(Join-Path $OutputDirectory 'fixes'))
    Invoke-Check 'fix-restart' @('run','--project','tests','-c','Release','--no-build','--','--fix-restart',(Join-Path $OutputDirectory 'fixes'))
    Invoke-Check 'vulnerabilities' @('list','src/GameClockOverlay.csproj','package','--vulnerable','--include-transitive')
    Invoke-Check 'test-vulnerabilities' @('list','tests/GameClockOverlay.Tests.csproj','package','--vulnerable','--include-transitive')
    Write-Output "Validation completed: $OutputDirectory"
}
finally { Pop-Location }
