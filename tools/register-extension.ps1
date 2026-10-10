#Requires -RunAsAdministrator
param([ValidateSet('Install','Uninstall')][string]$Action = 'Install')
$ErrorActionPreference = 'Stop'
$host = Join-Path $PSScriptRoot 'ApkShellext2.comhost.dll'
if (-not (Test-Path $host)) { throw "Missing modern COM host: $host" }
$regsvr = Join-Path $env:WINDIR 'System32\regsvr32.exe'
if ($Action -eq 'Install') { & $regsvr /s $host } else { & $regsvr /u /s $host }
if ($LASTEXITCODE -ne 0) { throw "COM host registration failed: $LASTEXITCODE" }
Write-Host "$Action .NET 10 COM host completed"
