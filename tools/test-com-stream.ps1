param([switch]$Legacy, [int]$TimeoutSeconds = 10)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repo 'ApkShellext2\Interop\ComStream.cs'
if ($Legacy) { $source = Join-Path $repo 'ApkShellext2\SharpShell\SharpShell\SharpShell\Helpers\ComStream.cs' }
$directory = Join-Path $repo 'tests\bin\com-stream'
New-Item -ItemType Directory -Force $directory | Out-Null
$exe = Join-Path $directory 'ComStreamRegression.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /out:$exe /r:System.IO.Compression.dll $source "$repo\tests\ComStreamRegression.cs"
if ($LASTEXITCODE -ne 0) { throw 'COM stream test compilation failed' }
$cases = @('offset', 'short_read', 'eof', 'initial_position', 'external_position', 'seek_end', 'seek_current', 'arguments', 'zero_count', 'null_constructor', 'read_only', 'managed_dispose', 'disposed_contract', 'native_dispose', 'native_shared', 'zip_reader')
$failures = 0
foreach ($case in $cases) {
    $process = Start-Process -FilePath $exe -ArgumentList $case -PassThru -NoNewWindow
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill(); $process.WaitForExit(); Write-Host "FAIL $case timeout"; $failures++
    } elseif ($process.ExitCode -ne 0) { $failures++ }
}
Write-Host "COM stream regressions: $($cases.Count - $failures)/$($cases.Count) passed"
if ($failures) { throw "$failures COM stream regressions failed" }
