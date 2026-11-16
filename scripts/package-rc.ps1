# GameClock Overlay（Avalonia）候選版及正式版封裝。
# 依專案既有設定：framework-dependent、win-x64、不啟用 Native AOT／Self-contained／單一檔案。
# 已存在的版本目錄或 ZIP 一律拒絕覆寫；本腳本不推送、不建立 Tag、不發布。
[CmdletBinding(DefaultParameterSetName = 'Candidate')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Official')][switch]$Official,
    [Parameter(ParameterSetName = 'Candidate')][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{0,40}$')][string]$Candidate = 'rc2',
    [string]$OutputRoot = 'dist'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\GameClockOverlay.csproj'
$xml = [xml](Get-Content -LiteralPath $project -Raw)
$version = @($xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Unexpected project version: $version" }
$label = if ($Official) { "v$version" } else { "v$version-$Candidate" }
$destination = Join-Path $root $OutputRoot
$packages = Join-Path $destination 'packages'
$publish = Join-Path $destination "$label\win-x64"
$zip = Join-Path $packages "GameClockOverlay-$label-win-x64.zip"
$sourceZip = Join-Path $packages "GameClockOverlay-$label-source.zip"
$sourceSums = Join-Path $packages "SOURCE-SHA256SUMS-$label.txt"
$zipSums = Join-Path $packages "SHA256SUMS-$label.txt"
if (Test-Path -LiteralPath $publish) { throw "Publish directory already exists: $publish" }
foreach ($artifact in @($zip, $sourceZip, $sourceSums, $zipSums)) {
    if (Test-Path -LiteralPath $artifact) { throw "Artifact already exists: $artifact" }
}
$gitCommit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Git checkout required' }
if (& git -C $root status --porcelain) { throw 'Commit the source before packaging' }

Push-Location $root
try {
    # DebugType=none：不產生 PDB，也不在 DLL 內嵌入本機 PDB 路徑。
    # 必須先完整重建：publish 會沿用 obj 內既有的編譯結果，單靠 publish 帶參數仍會留下舊的 PDB 路徑。
    $props = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-p:DebugType=none', '-p:DebugSymbols=false', '-p:RestoreLockedMode=true')
    & dotnet build $project @props --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    & dotnet publish $project @props --no-build -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
    # SkiaSharp／HarfBuzzSharp 原生套件會一併複製自己的 PDB；執行不需要，移除並記錄。
    foreach ($pdb in @(Get-ChildItem -LiteralPath $publish -Recurse -File -Filter '*.pdb')) {
        Write-Output "Removed debug symbols: $($pdb.Name)"
        Remove-Item -LiteralPath $pdb.FullName
    }
    foreach ($doc in 'README.md', 'CHANGELOG.md', "RELEASE-NOTES-$version.md") {
        Copy-Item -LiteralPath (Join-Path $root $doc) -Destination $publish
    }

    # 必要檔案。
    $required = @('GameClockOverlay.exe', 'GameClockOverlay.dll', 'GameClockOverlay.deps.json',
        'GameClockOverlay.runtimeconfig.json', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'README.md', 'CHANGELOG.md',
        "RELEASE-NOTES-$version.md", 'licenses\Avalonia-12.1.6-LICENSE.txt', 'licenses\MicroCom.Runtime-0.11.6-LICENSE.txt',
        'licenses\Tmds.DBus.Protocol-0.94.1-LICENSE.txt', 'licenses\dotnet-runtime-LICENSE.txt')
    foreach ($name in $required) {
        if (!(Test-Path -LiteralPath (Join-Path $publish $name) -PathType Leaf)) { throw "Missing: $name" }
    }

    # 禁止內容：PDB、測試、evidence、腳本、RTSS／Afterburner／Hook 二進位檔、SDK 原始碼。
    $files = @(Get-ChildItem -LiteralPath $publish -Recurse -File -Force)
    $forbidden = $files | Where-Object {
        $_.Extension -in '.pdb', '.ps1', '.cmd', '.bat', '.h', '.cpp', '.log' -or
        $_.Name -match '(?i)tests|evidence|probe|rtss|afterburner|msiab|hook|riva'
    }
    if ($forbidden) { throw "Forbidden files: $($forbidden.Name -join ', ')" }
    if (@(Get-ChildItem -LiteralPath $publish -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Reparse point in publish directory' }

    # 不得含有本機絕對路徑或使用者名稱（ASCII 與 UTF-16LE）。
    $user = [Environment]::UserName
    $needles = @($root, (Split-Path $root -Parent), $env:USERPROFILE, "\$user\") | Where-Object { $_ } | Select-Object -Unique
    foreach ($file in $files) {
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $ascii = [Text.Encoding]::ASCII.GetString($bytes); $wide = [Text.Encoding]::Unicode.GetString($bytes)
        foreach ($needle in $needles) {
            if ($ascii.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $wide.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "Local path or user name found in $($file.Name)"
            }
        }
    }

    # 版本、部署方式與架構。
    $info = (Get-Item -LiteralPath (Join-Path $publish 'GameClockOverlay.exe')).VersionInfo
    if ($info.ProductVersion -ne $version -or $info.FileVersion -ne "$version.0") { throw "Version mismatch: $($info.ProductVersion) / $($info.FileVersion)" }
    $dll = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $publish 'GameClockOverlay.dll'))
    if ($dll.Version -ne [Version]"$version.0") { throw "Assembly version mismatch: $($dll.Version)" }
    $config = Get-Content -LiteralPath (Join-Path $publish 'GameClockOverlay.runtimeconfig.json') -Raw | ConvertFrom-Json
    if ($config.runtimeOptions.includedFrameworks -or $config.runtimeOptions.framework.name -ne 'Microsoft.NETCore.App' -or $config.runtimeOptions.framework.version -ne '11.0.0') { throw 'Expected framework-dependent .NET 11 deployment' }
    $pe = [IO.File]::ReadAllBytes((Join-Path $publish 'GameClockOverlay.exe'))
    if ([BitConverter]::ToUInt16($pe, [BitConverter]::ToInt32($pe, 0x3c) + 4) -ne 0x8664) { throw 'Expected x64 apphost' }

    # 以 Git archive 的內容計算來源雜湊，避免 Windows 工作樹換行與 Git blob 不同。
    New-Item -ItemType Directory -Path $packages -Force | Out-Null
    & git -C $root archive --format=zip --output=$sourceZip $gitCommit
    if ($LASTEXITCODE -ne 0) { throw 'git archive failed' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($sourceZip)
    try {
        $sourceFiles = @($archive.Entries | Where-Object { $_.Name } | Sort-Object FullName)
        $sourceManifest = foreach ($entry in $sourceFiles) {
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $digest = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
            finally { $sha.Dispose(); $stream.Dispose() }
            "$digest  $($entry.FullName)"
        }
    }
    finally { $archive.Dispose() }
    $sourceManifest | Set-Content -LiteralPath $sourceSums -Encoding utf8
    [ordered]@{
        Version = $version; Candidate = $(if (!$Official) { $Candidate }); Product = 'GameClock Overlay'; UiFramework = 'Avalonia 12.1.6'
        TargetFramework = 'net11.0-windows'; CSharp = '15.0'; SDK = (& dotnet --version).Trim(); Runtime = 'Microsoft.NETCore.App 11.0.0'
        Deployment = 'framework-dependent'; RID = 'win-x64'; GitCommit = $gitCommit; AssemblyName = 'GameClockOverlay'; Executable = 'GameClockOverlay.exe'
        SourceManifestSha256 = (Get-FileHash -LiteralPath $sourceSums -Algorithm SHA256).Hash.ToLowerInvariant()
        SourceFileCount = $sourceFiles.Count; BuiltUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        ExternalDependency = 'RTSS (RivaTuner Statistics Server) is installed and started by the user; not bundled'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publish 'RELEASE-PROVENANCE.json') -Encoding utf8

    # 逐檔雜湊（SHA256SUMS.txt 本身除外），再壓縮。
    $files = @(Get-ChildItem -LiteralPath $publish -Recurse -File -Force | Sort-Object FullName)
    $manifest = foreach ($file in $files) {
        "$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($file.FullName.Substring($publish.Length + 1).Replace('\', '/'))"
    }
    $manifest | Set-Content -LiteralPath (Join-Path $publish 'SHA256SUMS.txt') -Encoding utf8
    Compress-Archive -LiteralPath $publish -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    @("$hash  $(Split-Path $zip -Leaf)", "$((Get-FileHash -LiteralPath $sourceZip -Algorithm SHA256).Hash.ToLowerInvariant())  $(Split-Path $sourceZip -Leaf)") | Set-Content -LiteralPath $zipSums -Encoding ascii
    Write-Output "Files: $($files.Count + 1)"
    Write-Output "Created: $zip"
    Write-Output "Size: $((Get-Item -LiteralPath $zip).Length)"
    Write-Output "SHA256: $hash"
}
finally { Pop-Location }
