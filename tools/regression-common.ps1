# Shared .NET 10 regression build/run support. Each run owns its output and logs.
function New-RegressionRunner {
    param([string]$Suite, [string]$AssemblyPath, [ValidateSet('x64','x86')][string]$Architecture = 'x64')
    $repo = Split-Path $PSScriptRoot -Parent
    $run = Join-Path $repo ('tests\bin\' + $Suite + '-' + $Architecture + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $run | Out-Null
    $arguments = @('build', "$repo\tests\Regression.csproj", '--configuration', 'Release', '--output', $run, '--nologo', "-p:Suite=$Suite", "-p:PlatformTarget=$Architecture", "-p:RuntimeIdentifier=win-$Architecture", "-p:BaseIntermediateOutputPath=$run\obj\")
    if ($Suite -ne 'ServiceRegression') {
        $assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
        $arguments += "-p:AssemblyPath=$assembly"
    }
    & dotnet @arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Suite .NET 10 compilation failed" }
    if ($Suite -ne 'ServiceRegression') {
        # Bring package dependencies/native libraries/resources beside the exact tested DLL.
        Get-ChildItem -LiteralPath (Split-Path $assembly) -File | Where-Object { $_.Extension -in '.dll','.json' -and $_.Name -notlike '*.runtimeconfig.json' -and $_.Name -notlike '*.deps.json' } | Copy-Item -Destination $run -Force
        Get-ChildItem -LiteralPath (Split-Path $assembly) -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'ApkShellext2.resources.dll') } | Copy-Item -Destination $run -Recurse -Force
        $expected = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath "$run\ApkShellext2.dll" -Algorithm SHA256).Hash -ne $expected) { throw 'Tested DLL differs from requested assembly' }
    }
    return [pscustomobject]@{ Exe = "$run\$Suite.exe"; Directory = $run; Sequence = 0 }
}
function Invoke-RegressionCase {
    param($Runner, [string[]]$Arguments = @(), [int]$TimeoutSeconds = 15)
    $Runner.Sequence++
    $log = Join-Path $Runner.Directory ('case-' + $Runner.Sequence)
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $Runner.Exe
    $start.Arguments = ($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    try {
        [void]$process.Start()
        # Drain both pipes asynchronously to avoid a full-buffer timeout/deadlock.
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $finished = $process.WaitForExit($TimeoutSeconds * 1000)
        if (-not $finished) { $process.Kill(); $process.WaitForExit() }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $stdout | Set-Content "$log.out"
        $stderr | Set-Content "$log.err"
        $stdout | Out-Host
        $stderr | Out-Host
        if (-not $finished) { Write-Host "FAIL $Arguments timeout ($TimeoutSeconds s)"; return $false }
        return $process.ExitCode -eq 0
    } finally { $process.Dispose() }
}
