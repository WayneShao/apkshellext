param([string]$AssemblyPath = "$PSScriptRoot\..\artifacts\build\win-x64\ApkShellext2.dll", [int]$TimeoutSeconds = 20)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\regression-common.ps1"
$runner = New-RegressionRunner -Suite 'MigrationUiRegression' -AssemblyPath $AssemblyPath
if (-not (Invoke-RegressionCase $runner -TimeoutSeconds $TimeoutSeconds)) { throw 'Migration UI regression failed' }
