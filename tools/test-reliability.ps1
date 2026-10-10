param([string]$AssemblyPath = "$PSScriptRoot\..\ApkShellext2\bin\Release\ApkShellext2.dll")
$ErrorActionPreference = 'Stop'
$run = Join-Path $PSScriptRoot ('..\tests\bin\run-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $run | Out-Null
Copy-Item $AssemblyPath "$run\ApkShellext2.dll"
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo "/out:$run\tests.exe" "/r:$run\ApkShellext2.dll" /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "$PSScriptRoot\..\tests\ReliabilityTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
$failed = 0
foreach ($case in @('unlock-apk','unlock-ipa','unlock-appx','unlock-appxbundle','upper','appx-desktop','bundle-eof','bundle-deflated','writer-sharing','version-invalid','update-first-run')) {
    $p = Start-Process "$run\tests.exe" -ArgumentList $case -PassThru -WindowStyle Hidden -RedirectStandardOutput "$run\$case.out" -RedirectStandardError "$run\$case.err"
    if (-not $p.WaitForExit(5000)) { $p.Kill(); $p.WaitForExit(); Write-Output "FAIL $case TIMEOUT"; $failed++ }
    else { $p.Refresh(); if ($p.ExitCode -ne 0) { $failed++ }; Get-Content "$run\$case.out"; Get-Content "$run\$case.err" | Select-Object -First 3 }
}
if ($failed) { throw "$failed regression scenarios failed; logs: $run" }
