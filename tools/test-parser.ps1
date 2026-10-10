param([string]$AssemblyPath = "$PSScriptRoot\..\artifacts\build\win-x64\ApkShellext2.dll", [int]$TimeoutSeconds = 5, [string[]]$ApkFixtures = @())
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\regression-common.ps1"
$runner = New-RegressionRunner -Suite 'ParserRegression' -AssemblyPath $AssemblyPath
$cases = @('boolean', 'xml', 'manifest', 'strings', 'resmap', 'fixture', 'sharing', 'ownership', 'bounds', 'cycle', 'resource_failure', 'staged_download', 'legacy_type', 'modern_dense', 'modern_sparse', 'modern_offset16', 'modern_type_offset', 'modern_errors', 'modern_sparse_max')
$failed = 0
foreach ($case in $cases) {
    if (-not (Invoke-RegressionCase $runner -Arguments @($case) -TimeoutSeconds $TimeoutSeconds)) { $failed++ }
}
foreach ($apk in $ApkFixtures) {
    if (-not (Invoke-RegressionCase $runner -Arguments @('real', (Resolve-Path -LiteralPath $apk).Path) -TimeoutSeconds $TimeoutSeconds)) { $failed++ }
}
$total = $cases.Count + $ApkFixtures.Count
Write-Host "ParserRegression regressions: $($total - $failed)/$total passed"
if ($failed) { throw "$failed regressions failed; logs: $($runner.Directory)" }
