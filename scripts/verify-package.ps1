# 獨立解壓縮並核對候選包；不啟動產品、不碰使用者設定或正式 RTSS。
param(
    [Parameter(Mandatory = $true)][string]$Zip,
    [Parameter(Mandatory = $true)][string]$ExtractTo,
    [Parameter(Mandatory = $true)][string]$PublishDirectory
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $ExtractTo) { throw "Extraction destination already exists: $ExtractTo" }
Expand-Archive -LiteralPath $Zip -DestinationPath $ExtractTo
$payload = Join-Path $ExtractTo 'win-x64'
if (!(Test-Path -LiteralPath $payload -PathType Container)) { throw 'Missing win-x64 directory' }
$actual = @(Get-ChildItem -LiteralPath $payload -Recurse -File)
$manifest = Get-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt')
$listed = @()
foreach ($line in $manifest) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw 'Invalid checksum line' }
    $expected = $Matches[1]; $relative = $Matches[2]
    if ([IO.Path]::IsPathRooted($relative) -or $relative.Split('/') -contains '..') { throw 'Invalid manifest path' }
    $file = Join-Path $payload $relative
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw "Checksum mismatch: $relative" }
    $listed += $relative
}
if (@($listed | Select-Object -Unique).Count -ne $listed.Count) { throw 'Duplicate manifest path' }
if ($actual.Count -ne $listed.Count + 1) { throw 'Unlisted file or missing file' }
$published = @(Get-ChildItem -LiteralPath $PublishDirectory -Recurse -File)
if ($published.Count -ne $actual.Count) { throw 'Publish file count mismatch' }
foreach ($file in $actual) {
    $relative = $file.FullName.Substring($payload.Length + 1)
    $source = Join-Path $PublishDirectory $relative
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $source).Hash) { throw "Publish mismatch: $relative" }
    if ($file.Extension -in '.pdb','.ps1','.h','.cpp','.log' -or $file.Name -match '(?i)tests|evidence|probe|rtss|afterburner|hook') { throw "Forbidden payload: $relative" }
}
$exe = Join-Path $payload 'GameClockOverlay.exe'
$info = (Get-Item -LiteralPath $exe).VersionInfo
$assembly = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $payload 'GameClockOverlay.dll'))
if ($info.ProductVersion -ne '0.3.0' -or $info.FileVersion -ne '0.3.0.0' -or $assembly.Name -ne 'GameClockOverlay' -or $assembly.Version -ne [version]'0.3.0.0') { throw 'Version or assembly mismatch' }
$runtime = Get-Content -LiteralPath (Join-Path $payload 'GameClockOverlay.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($runtime.runtimeOptions.framework.name -ne 'Microsoft.NETCore.App' -or $runtime.runtimeOptions.framework.version -ne '11.0.0' -or $runtime.runtimeOptions.includedFrameworks) { throw 'Runtime mismatch' }
$notices = Get-Content -LiteralPath (Join-Path $payload 'THIRD-PARTY-NOTICES.md') -Raw
foreach ($dll in @($actual | Where-Object Extension -eq '.dll')) {
    if ($dll.Name -like 'GameClockOverlay*' -or $dll.Name -like 'Avalonia*') { continue }
    if (!$notices.Contains($dll.Name)) { throw "Missing DLL notice: $($dll.Name)" }
}
foreach ($license in 'Avalonia-12.1.6-LICENSE.txt','MicroCom.Runtime-0.11.6-LICENSE.txt','Tmds.DBus.Protocol-0.94.1-LICENSE.txt') {
    if (!(Test-Path -LiteralPath (Join-Path $payload "licenses/$license"))) { throw "Missing license: $license" }
}
[ordered]@{
    Version = $info.ProductVersion; Files = $actual.Count; Checksums = $listed.Count
    PublishMatches = $true; ZipBytes = (Get-Item -LiteralPath $Zip).Length
    ZipSha256 = (Get-FileHash -LiteralPath $Zip).Hash.ToLowerInvariant()
    ExeSha256 = (Get-FileHash -LiteralPath $exe).Hash.ToLowerInvariant()
    GitCommit = (Get-Content -LiteralPath (Join-Path $payload 'RELEASE-PROVENANCE.json') -Raw | ConvertFrom-Json).GitCommit
} | ConvertTo-Json
