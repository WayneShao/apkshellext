param([string]$AssemblyPath = "$PSScriptRoot\..\artifacts\build\win-x64\ApkShellext2.dll", [int]$TimeoutSeconds = 10)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\regression-common.ps1"
$runner = New-RegressionRunner -Suite 'ComStreamRegression' -AssemblyPath $AssemblyPath
$cases = @('offset', 'short_read', 'eof', 'initial_position', 'external_position', 'seek_end', 'seek_current', 'arguments', 'zero_count', 'null_constructor', 'read_only', 'managed_dispose', 'disposed_contract', 'native_dispose', 'native_shared', 'zip_reader')
$failed = 0
foreach ($case in $cases) {
    if (-not (Invoke-RegressionCase $runner -Arguments @($case) -TimeoutSeconds $TimeoutSeconds)) { $failed++ }
}

Write-Host "ComStreamRegression regressions: $($cases.Count - $failed)/$($cases.Count) passed"
if ($failed) { throw "$failed regressions failed; logs: $($runner.Directory)" }
