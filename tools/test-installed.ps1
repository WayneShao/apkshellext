param([string]$ExpectedVersion = '0.3.0.0', [string]$InstalledAssembly)
$ErrorActionPreference = 'Stop'
if (-not $InstalledAssembly) {
    $registeredHost = (Get-Item 'Registry::HKEY_LOCAL_MACHINE\Software\Classes\CLSID\{1f869cee-4fda-35d9-896f-43975a87d1f6}\InprocServer32').GetValue('')
    $InstalledAssembly = Join-Path (Split-Path $registeredHost) 'ApkShellext2.dll'
}
$dir = Join-Path $PSScriptRoot ('..\tests\bin\installed-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $dir | Out-Null
$exe = Join-Path $dir 'ShellSmoke.exe'
dotnet build "$PSScriptRoot\..\tests\InstalledShellSmoke.csproj" -c Release -o $dir "-p:BaseIntermediateOutputPath=$dir\obj\" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Shell smoke compilation failed' }
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $exe
$start.Arguments = '"' + $ExpectedVersion + '" "' + $InstalledAssembly + '"'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$p = New-Object System.Diagnostics.Process
$p.StartInfo = $start
try {
    [void]$p.Start()
    $stdout = $p.StandardOutput.ReadToEndAsync()
    $stderr = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit(20000)) { $p.Kill(); $p.WaitForExit(); throw 'Shell callback timed out' }
    $stdout.GetAwaiter().GetResult() | Write-Host
    $stderr.GetAwaiter().GetResult() | Write-Host
    if ($p.ExitCode -ne 0) { throw 'Installed Shell callback test failed' }
} finally { $p.Dispose() }
