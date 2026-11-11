param([ValidatePattern('^$|^[A-Za-z0-9][A-Za-z0-9.-]{0,40}$')][string]$Candidate = '', [string]$OutputRoot = 'dist')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $root $OutputRoot
$packages = Join-Path $destination 'packages'
$version = ([xml](Get-Content (Join-Path $root 'src\GameClockOverlay.csproj') -Raw)).Project.PropertyGroup.Version
$label = if ($Candidate) { "v$version-$Candidate" } else { "v$version" }
$publish = Join-Path $destination "$label\win-x64"
$zip = Join-Path $packages "GameClockOverlay-$label-win-x64.zip"
if (Test-Path -LiteralPath $publish) { throw "Publish directory already exists: $publish" }
if (Test-Path -LiteralPath $zip) { throw "Package already exists: $zip" }
Push-Location $root
try {
    & dotnet publish src/GameClockOverlay.csproj -c Release -r win-x64 --self-contained false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
    $required = @('GameClockOverlay.exe','GameClockOverlay.dll','GameClockOverlay.deps.json','GameClockOverlay.runtimeconfig.json','README.md','CHANGELOG.md','MANUAL-ACCEPTANCE.md','UPGRADE-REPORT.md','LICENSE','THIRD-PARTY-NOTICES.txt','licenses\dotnet-runtime-LICENSE.txt','licenses\dotnet-runtime-THIRD-PARTY-NOTICES.txt')
    foreach ($name in $required) {
        if (!(Test-Path -LiteralPath (Join-Path $publish $name) -PathType Leaf)) { throw "Missing: $name" }
    }
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit' }
    if (!$Candidate) {
        if (& git status --porcelain) { throw 'Formal release requires a clean working tree' }
        $tagCommit = (& git rev-parse "v$version^{commit}").Trim()
        if ($LASTEXITCODE -ne 0 -or $tagCommit -ne $commit) { throw 'Release tag must point to HEAD before packaging' }
    }
    [ordered]@{ Version=$version; Commit=$commit; Tree=(& git rev-parse 'HEAD^{tree}').Trim(); Tag="v$version"; TargetFramework='net11.0-windows'; CSharp='15.0'; SDK=(& dotnet --version).Trim(); Deployment='framework-dependent'; RID='win-x64' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publish 'RELEASE-PROVENANCE.json') -Encoding utf8
    $required += 'RELEASE-PROVENANCE.json'
    $files = @(Get-ChildItem -LiteralPath $publish -Recurse -File -Force)
    if ($files.Count -ne $required.Count) { throw 'Unexpected publish files; review before packaging' }
    if (@(Get-ChildItem -LiteralPath $publish -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Reparse point in publish directory' }
    $exeVersion = (Get-Item (Join-Path $publish 'GameClockOverlay.exe')).VersionInfo.ProductVersion
    if ($exeVersion -ne $version) { throw "Version mismatch: $exeVersion / $version" }
    $config = Get-Content (Join-Path $publish 'GameClockOverlay.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (!$config.runtimeOptions.frameworks -or $config.runtimeOptions.includedFrameworks) { throw 'Expected framework-dependent deployment' }
    if ($config.runtimeOptions.tfm -ne 'net11.0' -or !($config.runtimeOptions.frameworks | Where-Object { $_.name -eq 'Microsoft.WindowsDesktop.App' -and $_.version -eq '11.0.0' })) { throw 'Expected .NET 11 Windows Desktop framework' }
    $deps = Get-Content (Join-Path $publish 'GameClockOverlay.deps.json') -Raw | ConvertFrom-Json
    if (@($deps.libraries.PSObject.Properties).Count -ne 1) { throw 'Unexpected dependencies; review inventory' }
    $pe = [IO.File]::ReadAllBytes((Join-Path $publish 'GameClockOverlay.exe'))
    $offset = [BitConverter]::ToInt32($pe, 0x3c)
    if ([BitConverter]::ToUInt16($pe, $offset + 4) -ne 0x8664) { throw 'Expected x64 apphost' }
    $manifest = foreach ($file in $files | Sort-Object FullName) {
        $relative = $file.FullName.Substring($publish.Length + 1).Replace('\', '/')
        $fileHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$fileHash  $relative"
    }
    $manifest | Set-Content -LiteralPath (Join-Path $publish 'SHA256SUMS.txt') -Encoding utf8
    New-Item -ItemType Directory -Path $packages -Force | Out-Null
    Compress-Archive -LiteralPath $publish -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path $zip -Leaf)" | Set-Content -LiteralPath (Join-Path $packages "SHA256SUMS-$label.txt") -Encoding ascii
    Write-Output "Created: $zip"
    Write-Output "SHA256: $hash"
}
finally { Pop-Location }
