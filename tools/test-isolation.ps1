param(
 [string]$BridgePath = "$PSScriptRoot\..\artifacts\isolated\package\ApkShellext2.Native.dll",
 [Parameter(Mandatory=$true)][string]$ApkFixture,
 [Parameter(Mandatory=$true)][string]$ZigPath,
 [int]$TimeoutSeconds = 90,
 [switch]$SkipFaults,
 [switch]$Registered
)
$ErrorActionPreference='Stop'
$candidate=(Resolve-Path -LiteralPath $BridgePath).Path
$fixture=(Resolve-Path -LiteralPath $ApkFixture).Path
$run=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\tests\bin\isolation-'+[guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Force $run | Out-Null
$beforeHash=(Get-FileHash -LiteralPath $fixture).Hash
Get-FileHash -LiteralPath $candidate,$fixture | Format-List | Out-File "$run\input-hashes.txt"
$native=Join-Path $run 'IsolationNative.exe'
$library=Join-Path $run 'IsolationNative.dll'
$source="$PSScriptRoot\..\tests\IsolationNative.cpp"
$common=@('c++','-target','x86_64-windows-gnu','-std=c++17','-O1',$source,'-lole32','-luuid','-lshell32','-lshlwapi','-lgdi32','-luser32')
& $ZigPath @common -municode -o $native
if($LASTEXITCODE -ne 0){throw 'Native isolation harness build failed'}
& $ZigPath @common -shared -DISOLATION_DLL -o $library
if($LASTEXITCODE -ne 0){throw 'Managed isolation harness DLL build failed'}
foreach($framework in @('net8.0','net10.0')) {
 & dotnet build "$PSScriptRoot\..\tests\IsolationManaged.csproj" -c Release -f $framework -o "$run\$framework" --nologo
 if($LASTEXITCODE -ne 0){throw "Managed harness build failed: $framework"}
}
function Invoke-Probe([string]$Label,[string]$Exe,[string[]]$Arguments) {
 if($Registered -and $Label -notin @("missing","crash","timeout")){$Arguments+=@("--registered")}
 $start=New-Object Diagnostics.ProcessStartInfo
 $start.FileName=$Exe
 # Paths are ordinary Windows filenames and cannot contain quotes.
 $start.Arguments=($Arguments | ForEach-Object {'"'+$_+'"'}) -join ' '
 $start.UseShellExecute=$false; $start.CreateNoWindow=$true
 $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
 $p=New-Object Diagnostics.Process; $p.StartInfo=$start
 try {
  [void]$p.Start(); $stdoutTask=$p.StandardOutput.ReadToEndAsync(); $stderrTask=$p.StandardError.ReadToEndAsync()
  if(-not $p.WaitForExit($TimeoutSeconds*1000)){try{$p.Kill($true)}catch{$p.Kill()};$p.WaitForExit();throw "Probe timeout: $Label"}
  $stdout=$stdoutTask.GetAwaiter().GetResult();$stderr=$stderrTask.GetAwaiter().GetResult()
  $stdout | Set-Content "$run\$Label.stdout.txt"; $stderr | Set-Content "$run\$Label.stderr.txt"
  Write-Output "=== $Label ==="; Write-Output $stdout; if($stderr){Write-Output $stderr}
  if($p.ExitCode -ne 0){throw "Probe failed: $Label (exit $($p.ExitCode)); evidence $run"}
 } finally {$p.Dispose()}
}
# Preserve exact existing thumbnail preference, including absent value/key and registry kind.
$settingsPath='SOFTWARE\ApkShellext2'
$settings=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($settingsPath,$true)
$keyExisted=$null -ne $settings
if(-not $settings){$settings=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($settingsPath)}
$valueExisted=$settings.GetValueNames() -contains 'EnableThumbnail'
$oldValue=$null;$oldKind=$null
if($valueExisted){$oldValue=$settings.GetValue('EnableThumbnail',$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames);$oldKind=$settings.GetValueKind('EnableThumbnail')}
try {
$settings.SetValue('EnableThumbnail','True',[Microsoft.Win32.RegistryValueKind]::String)
Invoke-Probe 'native' $native @($candidate,$fixture)
$dotnet=(Get-Command dotnet).Source
foreach($framework in @('net8.0','net10.0')) {Invoke-Probe $framework $dotnet @("$run\$framework\IsolationManaged.dll",$library,$candidate,$fixture)}
$settings.SetValue('EnableThumbnail','False',[Microsoft.Win32.RegistryValueKind]::String)
Invoke-Probe 'thumbnail-disabled' $native @($candidate,$fixture,'--disabled')
$settings.SetValue('EnableThumbnail','True',[Microsoft.Win32.RegistryValueKind]::String)
if(-not $SkipFaults) {
 foreach($fault in @('missing','crash','timeout')) {
  $dir=Join-Path $run $fault; New-Item -ItemType Directory $dir | Out-Null
  # Only the native module and a synthetic sibling are needed for these fault fixtures.
  Copy-Item -LiteralPath $candidate -Destination "$dir\ApkShellext2.Native.dll"
  if($fault -ne 'missing') {
   $flags=@();if($fault -eq 'timeout'){$flags=@('-DHANG_WORKER')}
   & $ZigPath c++ -target x86_64-windows-gnu -O1 "$PSScriptRoot\..\tests\IsolationFaultWorker.cpp" @flags -o "$dir\ApkShellext2.Worker.exe"
   if($LASTEXITCODE -ne 0){throw "Fault fixture compilation failed: $fault"}
  }
  Invoke-Probe $fault $native @("$dir\ApkShellext2.Native.dll",$fixture,'--fault')
 }
}
if((Get-FileHash -LiteralPath $fixture).Hash -ne $beforeHash){throw "Input APK changed"}
Write-Output "PASS isolated native, .NET 8, .NET 10 hosts; evidence: $run"


} finally {
 if($valueExisted){$settings.SetValue('EnableThumbnail',$oldValue,$oldKind)}else{$settings.DeleteValue('EnableThumbnail',$false)}
 $settings.Dispose()
 if(-not $keyExisted){
  $check=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($settingsPath)
  $empty=$check -and $check.ValueCount -eq 0 -and $check.SubKeyCount -eq 0
  if($check){$check.Dispose()}
  if($empty){[Microsoft.Win32.Registry]::CurrentUser.DeleteSubKey($settingsPath,$false)}
 }
}
