param([string]$AssemblyPath = "$PSScriptRoot\..\artifacts\build\win-x64\ApkShellext2.dll", [int]$TimeoutSeconds = 15, [string]$PreviewPath = "")
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\regression-common.ps1"
$runner = New-RegressionRunner -Suite 'OverlayRegression' -AssemblyPath $AssemblyPath
$arguments = @()
if ($PreviewPath) { $arguments += $PreviewPath }
if (-not (Invoke-RegressionCase $runner -Arguments $arguments -TimeoutSeconds $TimeoutSeconds)) { throw 'OverlayRegression regression failed' }
