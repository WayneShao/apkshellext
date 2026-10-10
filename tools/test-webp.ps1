param([string]$AssemblyPath = "$PSScriptRoot\..\artifacts\build\win-x64\ApkShellext2.dll", [int]$TimeoutSeconds = 15, [ValidateSet('x64','x86')][string[]]$Architecture = @('x64','x86'))
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\regression-common.ps1"
$fixture = (Resolve-Path "$PSScriptRoot\..\tests\fixtures\alpha-lossless.webp").Path
$failed = 0
foreach ($arch in $Architecture) {
    $runner = New-RegressionRunner -Suite 'WebPRegression' -AssemblyPath $AssemblyPath -Architecture $arch
    if (-not (Invoke-RegressionCase $runner -Arguments @($fixture) -TimeoutSeconds $TimeoutSeconds)) { $failed++ }
}
if ($failed) { throw "$failed WebP regression processes failed" }
