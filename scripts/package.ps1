$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root 'dist\win-x64'
$packages = Join-Path $root 'dist\packages'
$version = ([xml](Get-Content (Join-Path $root 'src\GameClockOverlay.csproj') -Raw)).Project.PropertyGroup.Version
$zip = Join-Path $packages "GameClockOverlay-v$version-win-x64.zip"
if (Test-Path -LiteralPath $zip) { throw "Package already exists: $zip" }
Push-Location $root
try {
    & dotnet publish src/GameClockOverlay.csproj -c Release --self-contained false -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
    $required = @('GameClockOverlay.exe','GameClockOverlay.dll','GameClockOverlay.deps.json','GameClockOverlay.runtimeconfig.json','README.md','LICENSE','THIRD-PARTY-NOTICES.txt','licenses\dotnet-runtime-LICENSE.txt','licenses\dotnet-runtime-THIRD-PARTY-NOTICES.txt')
    foreach ($name in $required) {
        if (!(Test-Path -LiteralPath (Join-Path $publish $name) -PathType Leaf)) { throw "Missing: $name" }
    }
    $files = @(Get-ChildItem -LiteralPath $publish -Recurse -File -Force)
    if ($files.Count -ne $required.Count) { throw 'Unexpected publish files; review before packaging' }
    if (@(Get-ChildItem -LiteralPath $publish -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Reparse point in publish directory' }
    $exeVersion = (Get-Item (Join-Path $publish 'GameClockOverlay.exe')).VersionInfo.ProductVersion
    if ($exeVersion -ne $version) { throw "Version mismatch: $exeVersion / $version" }
    $config = Get-Content (Join-Path $publish 'GameClockOverlay.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (!$config.runtimeOptions.frameworks -or $config.runtimeOptions.includedFrameworks) { throw 'Expected framework-dependent deployment' }
    $pe = [IO.File]::ReadAllBytes((Join-Path $publish 'GameClockOverlay.exe'))
    $offset = [BitConverter]::ToInt32($pe, 0x3c)
    if ([BitConverter]::ToUInt16($pe, $offset + 4) -ne 0x8664) { throw 'Expected x64 apphost' }
    New-Item -ItemType Directory -Path $packages -Force | Out-Null
    Compress-Archive -LiteralPath $publish -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path $zip -Leaf)" | Set-Content -LiteralPath (Join-Path $packages 'SHA256SUMS') -Encoding ascii
    Write-Output "Created: $zip"
    Write-Output "SHA256: $hash"
}
finally { Pop-Location }
