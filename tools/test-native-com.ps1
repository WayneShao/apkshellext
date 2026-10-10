param(
 [string]$HostPath,
 [Parameter(Mandatory=$true)][string]$ApkFixture,
 [Parameter(Mandatory=$true)][string]$ZigPath,
 [int]$TimeoutSeconds = 60,
 [switch]$Registered
)
$ErrorActionPreference='Stop'
if (-not $HostPath) { $HostPath=Join-Path $PSScriptRoot '..\artifacts\build\win-x64\ApkShellext2.comhost.dll' }
$candidate=(Resolve-Path -LiteralPath $HostPath).Path
$fixture=(Resolve-Path -LiteralPath $ApkFixture).Path
$run=Join-Path $PSScriptRoot ('..\tests\bin\native-com-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $run | Out-Null
$exe=Join-Path $run 'NativeComSmoke.exe'
& $ZigPath c++ -target x86_64-windows-gnu -municode -std=c++17 -O1 "$PSScriptRoot\..\tests\NativeComSmoke.cpp" -lole32 -luuid -lshell32 -lshlwapi -lgdi32 -luser32 -o $exe
if($LASTEXITCODE -ne 0){throw 'Native COM harness compilation failed'}
$start=New-Object System.Diagnostics.ProcessStartInfo
$start.FileName=$exe
$start.Arguments='"'+$candidate+'" "'+$fixture+'"'
if ($Registered) { $start.Arguments += " --registered" }
$start.UseShellExecute=$false
$start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true
$start.RedirectStandardError=$true
$p=New-Object System.Diagnostics.Process
$p.StartInfo=$start
try {
 [void]$p.Start()
 if(-not $p.WaitForExit($TimeoutSeconds*1000)){$p.Kill();$p.WaitForExit();throw 'Native COM host timeout'}
 $stdout=$p.StandardOutput.ReadToEnd();$stderr=$p.StandardError.ReadToEnd()
 $stdout | Set-Content "$run\stdout.txt"; $stderr | Set-Content "$run\stderr.txt"
 Write-Output $stdout
 if($stderr){Write-Output $stderr}
 if($p.ExitCode -ne 0){throw "Native COM host failed: $run"}
} finally {$p.Dispose()}
