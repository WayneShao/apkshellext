param([switch]$Legacy, [string]$AssemblyPath = "$PSScriptRoot\..\ApkShellext2\bin\Release\ApkShellext2.dll", [string]$PreviewPath = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$assembly = (Resolve-Path $AssemblyPath).Path
$exe = Join-Path (Split-Path $assembly) 'OverlayRegression.exe'
$sources = @("$repo\tests\OverlayRegression.cs")
$options = @()
if ($Legacy) { $options += '/define:LEGACY' } else { $sources += "$repo\ApkShellext2\TypeOverlay.cs" }
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /out:$exe /r:$assembly /r:System.Drawing.dll @options @sources
if ($LASTEXITCODE -ne 0) { throw 'Overlay test compilation failed' }
if ($PreviewPath) { & $exe $PreviewPath } else { & $exe }
if ($LASTEXITCODE -ne 0) { throw 'Overlay regression failed' }
