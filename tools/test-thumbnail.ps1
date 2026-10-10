param([string]$AssemblyPath = "$PSScriptRoot\..\ApkShellext2\bin\Release\ApkShellext2.dll")
$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path $AssemblyPath).Path
$exe = Join-Path (Split-Path $assembly) 'ThumbnailRegression.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /out:$exe /r:$assembly /r:System.Drawing.dll "$PSScriptRoot\..\tests\ThumbnailRegression.cs"
if ($LASTEXITCODE -ne 0) { throw 'Thumbnail integration test compilation failed' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Thumbnail integration regression failed' }
