param([string]$ExpectedVersion = '0.2.0.8')
$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot '..\tests\bin\installed'
New-Item -ItemType Directory -Force $dir | Out-Null
$exe = Join-Path $dir 'ShellSmoke.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /out:$exe "$PSScriptRoot\..\tests\ShellSmoke.cs"
if ($LASTEXITCODE -ne 0) { throw 'Shell smoke compilation failed' }
$p = Start-Process $exe -ArgumentList $ExpectedVersion -PassThru -NoNewWindow
if (-not $p.WaitForExit(20000)) { $p.Kill(); $p.WaitForExit(); throw 'Shell callback timed out' }
if ($p.ExitCode -ne 0) { throw 'Installed Shell callback test failed' }
