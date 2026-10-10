#Requires -RunAsAdministrator
param([ValidateSet('Install','Uninstall')][string]$Action = 'Install')
$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot 'ApkShellext2.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw "Missing assembly: $dll" }
$framework = if ([Environment]::Is64BitOperatingSystem) { 'Framework64' } else { 'Framework' }
$regasm = Join-Path $env:WINDIR "Microsoft.NET\$framework\v4.0.30319\RegAsm.exe"
if ($Action -eq 'Install') { & $regasm /codebase $dll }
else { & $regasm /unregister $dll }
if ($LASTEXITCODE -ne 0) { throw "Registration failed: $LASTEXITCODE" }
Write-Host "$Action completed. Restart applications that loaded the previous extension before testing."
