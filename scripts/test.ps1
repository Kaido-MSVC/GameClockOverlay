# 執行隔離回歸；不使用正式 RTSS 映射，不啟動遊戲。
param(
    [ValidateSet('Debug','Release')][string[]]$Configurations = @('Debug','Release'),
    [string]$EvidenceRoot = 'evidence/regression'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($configuration in $Configurations) {
    $runner = Join-Path $root "tests/bin/$configuration/net11.0-windows/win-x64/GameClockOverlay.Tests.exe"
    if (!(Test-Path -LiteralPath $runner)) { throw "Build the $configuration test runner first" }
    foreach ($mode in 'ui','hotkey-conflict','quit-fault','session-end','review','rtss-launch-ui') {
        $output = [IO.Path]::GetFullPath((Join-Path $root "$EvidenceRoot/$configuration/$mode"))
        if (Test-Path -LiteralPath $output) { throw "Evidence already exists: $output" }
        New-Item -ItemType Directory -Path $output -Force | Out-Null
        $arguments = @($output)
        if ($mode -ne 'ui') { $arguments += "--$mode" }
        & $runner @arguments *> (Join-Path $output 'console.txt')
        if ($LASTEXITCODE -ne 0) { throw "Test failed: $configuration/$mode (exit $LASTEXITCODE)" }
        $summary = Get-Content -LiteralPath (Join-Path $output 'summary.json') -Raw | ConvertFrom-Json
        if ($summary.failed -ne 0) { throw "Failed checks: $configuration/$mode" }
        Write-Output "$configuration/$mode PASS=$($summary.passed) FAIL=$($summary.failed)"
    }
}
