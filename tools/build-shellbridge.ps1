param(
    [string]$Zig = 'D:\Backup\Desktop\apkshellext-analysis\native-build\zig\zig-x86_64-windows-0.15.2\zig.exe',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\isolated\shell')
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$output = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'ApkShellext2.Native.dll'
& $Zig c++ -target x86_64-windows-gnu -std=c++17 -O2 -shared (Join-Path $root 'native\shellbridge\bridge.cpp') -o $output -lole32 -luuid -lshell32 -luser32 -lgdi32 -lbcrypt
if ($LASTEXITCODE -ne 0) { throw "Native shell bridge build failed: $LASTEXITCODE" }
$header = [IO.File]::ReadAllBytes($output)
if ($header.Length -lt 64 -or $header[0] -ne 0x4d -or $header[1] -ne 0x5a) {
    throw 'Native bridge output is not a Windows PE image'
}
Get-Item $output
