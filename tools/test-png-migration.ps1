$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\regression-common.ps1"
$runner = New-RegressionRunner -Suite PngMigrationRegression -AssemblyPath "$PSScriptRoot\..\artifacts\build\win-x64\ApkShellext2.dll"
$ok = Invoke-RegressionCase $runner
if (-not $ok) { throw 'PNG migration regression failed' }
