param([string]$AssemblyPath = "$PSScriptRoot\..\artifacts\build\win-x64\ApkShellext2.dll", [int]$TimeoutSeconds = 5)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\regression-common.ps1"
$runner = New-RegressionRunner -Suite 'ReliabilityTests' -AssemblyPath $AssemblyPath
$cases = @('unlock-apk', 'unlock-ipa', 'unlock-appx', 'unlock-appxbundle', 'upper', 'appx-desktop', 'bundle-eof', 'bundle-deflated', 'writer-sharing', 'version-invalid', 'update-first-run')
$failed = 0
foreach ($case in $cases) {
    if (-not (Invoke-RegressionCase $runner -Arguments @($case) -TimeoutSeconds $TimeoutSeconds)) { $failed++ }
}

Write-Host "ReliabilityTests regressions: $($cases.Count - $failed)/$($cases.Count) passed"
if ($failed) { throw "$failed regressions failed; logs: $($runner.Directory)" }
