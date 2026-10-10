$ErrorActionPreference = 'Stop'
$run = Join-Path $PSScriptRoot ('..\tests\bin\service-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $run | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /main:ServiceRegression "/out:$run\tests.exe" /r:System.ServiceProcess.dll "$PSScriptRoot\..\ApkShellextService\apkShellextService.cs" "$PSScriptRoot\..\tests\ServiceRegression.cs"
if ($LASTEXITCODE -ne 0) { throw 'Service test compilation failed' }
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = "$run\tests.exe"
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$p = New-Object System.Diagnostics.Process
$p.StartInfo = $start
try {
    [void]$p.Start()
    if (-not $p.WaitForExit(15000)) { $p.Kill(); $p.WaitForExit(); throw "Service regression timed out: $run" }
    $stdout = $p.StandardOutput.ReadToEnd()
    $stderr = $p.StandardError.ReadToEnd()
    $stdout | Set-Content "$run\tests.out"
    $stderr | Set-Content "$run\tests.err"
    Write-Output $stdout
    if ($stderr) { Write-Output $stderr }
    if ($p.ExitCode -ne 0) { throw "Service regression failed: $run" }
} finally { $p.Dispose() }
