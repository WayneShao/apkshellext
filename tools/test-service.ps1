param([string]$AssemblyPath = "$PSScriptRoot\..\artifacts\build\win-x64\ApkShellext2.dll", [int]$TimeoutSeconds = 15)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\regression-common.ps1"
$runner = New-RegressionRunner -Suite 'ServiceRegression' -AssemblyPath $AssemblyPath
$arguments = @()

if (-not (Invoke-RegressionCase $runner -Arguments $arguments -TimeoutSeconds $TimeoutSeconds)) { throw 'ServiceRegression regression failed' }
