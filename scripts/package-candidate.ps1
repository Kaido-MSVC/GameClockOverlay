param(
    [ValidatePattern('^$|^[A-Za-z0-9][A-Za-z0-9.-]{0,40}$')][AllowEmptyString()][string]$Candidate = 'candidate',
    [string]$OutputRoot = 'dist',
    [Parameter(Mandatory=$true)][string]$ValidationDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$ValidationDirectory = [IO.Path]::GetFullPath($ValidationDirectory)
foreach ($name in @('build','main','display','lifecycle','reload','instances','fixes','fix-restart','vulnerabilities','test-vulnerabilities')) {
    if (!(Test-Path -LiteralPath (Join-Path $ValidationDirectory "$name.log"))) { throw "Missing validation log: $name" }
}
$version = ([xml](Get-Content (Join-Path $root 'src/GameClockOverlay.csproj') -Raw)).Project.PropertyGroup.Version
$label = if ($Candidate) { "v$version-$Candidate" } else { "v$version" }
$destination = Join-Path $root $OutputRoot
$packages = Join-Path $destination 'packages'
$staging = Join-Path $root "artifacts/package-$label"
$kit = Join-Path $staging 'GameClockOverlay-tests'
$source = Join-Path $staging 'GameClockOverlay-source'
$testZip = Join-Path $packages "GameClockOverlay-$label-tests-win-x64.zip"
$sourceZip = Join-Path $packages "GameClockOverlay-$label-source.zip"
foreach ($path in @($staging,$testZip,$sourceZip)) { if (Test-Path -LiteralPath $path) { throw "Already exists: $path" } }
Push-Location $root
try {
    & (Join-Path $PSScriptRoot 'package.ps1') -Candidate $Candidate -OutputRoot $OutputRoot
    New-Item -ItemType Directory -Path $kit | Out-Null
    $runtime = Join-Path $kit 'win-x64'
    & dotnet publish tests/GameClockOverlay.Tests.csproj -c Release -r win-x64 --self-contained false -p:DebugType=embedded -o $runtime
    if ($LASTEXITCODE -ne 0) { throw 'Test runner publish failed' }
    $releaseDll = Join-Path $destination "$label/win-x64/GameClockOverlay.dll"
    if ((Get-FileHash $releaseDll).Hash -ne (Get-FileHash (Join-Path $runtime 'GameClockOverlay.dll')).Hash) { throw 'Release / runner app DLL mismatch' }
    if (& git status --porcelain) { throw 'Source archive requires a clean committed tree' }
    & git archive --format=zip --prefix=GameClockOverlay-source/ --output=$sourceZip HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Git source archive failed' }
    Expand-Archive -LiteralPath $sourceZip -DestinationPath $staging
    Copy-Item -LiteralPath (Join-Path (Split-Path $releaseDll -Parent) 'RELEASE-PROVENANCE.json') -Destination $runtime
    Copy-Item -LiteralPath $source -Destination (Join-Path $kit 'source') -Recurse
    $records = Join-Path $kit 'records'
    New-Item -ItemType Directory -Path $records | Out-Null
    Get-ChildItem -LiteralPath $ValidationDirectory -Filter '*.log' -File | Copy-Item -Destination $records
    Get-ChildItem -LiteralPath (Join-Path $ValidationDirectory 'lifecycle') -Filter '*.png' -File | Copy-Item -Destination $records
    @'
GameClockOverlay 0.2.0 validation kit
Requires Windows 11 x64 and .NET 11 Windows Desktop Runtime x64.
Run Run-Tests.ps1. It uses a fresh results folder and isolated settings.
Close an already-running GameClockOverlay before testing its mutex and hotkey.
source/ contains rebuildable sources; records/ contains the recorded validation.
Synthetic drag / modal-dialog tests do not replace real mouse / game acceptance.
See win-x64/MANUAL-ACCEPTANCE.md for user-reported acceptance and equipment limitations.
'@ | Set-Content -LiteralPath (Join-Path $kit 'TEST-README.txt') -Encoding utf8
    @'
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$run = Join-Path $PSScriptRoot ('results/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$runner = Join-Path $PSScriptRoot 'win-x64/GameClockOverlay.Tests.exe'
function Invoke-Runner([string[]]$runnerArguments) {
    & $runner @runnerArguments
    if ($LASTEXITCODE -ne 0) { throw "Runner failed ($LASTEXITCODE)" }
}
Invoke-Runner @((Join-Path $run 'main'))
Invoke-Runner @('--display')
Invoke-Runner @('--lifecycle',(Join-Path $run 'lifecycle'))
Invoke-Runner @('--lifecycle',(Join-Path $run 'lifecycle'),'reload')
Invoke-Runner @('--instances')
Invoke-Runner @('--fixes',(Join-Path $run 'fixes'))
Invoke-Runner @('--fix-restart',(Join-Path $run 'fixes'))
Write-Output "All runner checks passed. Results: $run"
'@ | Set-Content -LiteralPath (Join-Path $kit 'Run-Tests.ps1') -Encoding utf8
    $manifest = foreach ($file in Get-ChildItem -LiteralPath $kit -File -Recurse | Sort-Object FullName) {
        $relative = $file.FullName.Substring($kit.Length + 1).Replace('\','/')
        "$((Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant())  $relative"
    }
    $manifest | Set-Content -LiteralPath (Join-Path $kit 'SHA256SUMS.txt') -Encoding utf8
    Compress-Archive -LiteralPath $kit -DestinationPath $testZip -CompressionLevel Optimal
    foreach ($zip in @($sourceZip,$testZip)) {
        "$((Get-FileHash -LiteralPath $zip).Hash.ToLowerInvariant())  $(Split-Path $zip -Leaf)" | Add-Content -LiteralPath (Join-Path $packages "SHA256SUMS-$label.txt") -Encoding ascii
    }
    Write-Output "Packages ready: $label"
}
finally { Pop-Location }
