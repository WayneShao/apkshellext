#Requires -RunAsAdministrator
param([ValidateSet('Install','Uninstall')][string]$Action = 'Install')
$ErrorActionPreference = 'Stop'
$comHostPath = Join-Path $PSScriptRoot 'ApkShellext2.comhost.dll'
if (-not (Test-Path -LiteralPath $comHostPath)) { throw "Missing modern COM host: $comHostPath" }
if (-not [Environment]::Is64BitOperatingSystem) { throw 'This package requires Windows x64.' }
$runtimeRoot = Join-Path $env:ProgramW6432 'dotnet\shared\Microsoft.WindowsDesktop.App'
$runtime = Get-ChildItem -LiteralPath $runtimeRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^10\.0\.\d+$' }
if ($Action -eq 'Install' -and -not $runtime) {
    throw 'Install the x64 .NET 10 Desktop Runtime before registering: https://dotnet.microsoft.com/download/dotnet/10.0'
}
$regsvr = Join-Path $env:WINDIR $(if ([Environment]::Is64BitProcess) { 'System32\regsvr32.exe' } else { 'Sysnative\regsvr32.exe' })
$arguments = if ($Action -eq 'Install') { '/s "' + $comHostPath + '"' } else { '/u /s "' + $comHostPath + '"' }
$process = Start-Process -FilePath $regsvr -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "COM host registration failed: $($process.ExitCode)" }
Write-Host "$Action .NET 10 COM host completed"
