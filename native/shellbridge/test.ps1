param([string]$Zig = 'D:\Backup\Desktop\apkshellext-analysis\native-build\zig\zig-x86_64-windows-0.15.2\zig.exe')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$out = Join-Path $root 'artifacts\isolated\bridge-tests'
New-Item -ItemType Directory -Force $out | Out-Null
& $Zig c++ -target x86_64-windows-gnu -municode -std=c++17 -O2 (Join-Path $PSScriptRoot 'MockWorker.cpp') -o (Join-Path $out 'ApkShellext2.Worker.exe')
if ($LASTEXITCODE -ne 0) { throw 'Mock worker compilation failed' }
& $Zig c++ -target x86_64-windows-gnu -municode -std=c++17 -O2 (Join-Path $PSScriptRoot 'BridgeSmoke.cpp') -o (Join-Path $out 'BridgeSmoke.exe') -lole32 -luuid -lshlwapi -lshell32 -luser32 -lgdi32
if ($LASTEXITCODE -ne 0) { throw 'Native regression compilation failed' }
& $Zig c++ -target x86_64-windows-gnu -municode -std=c++17 -O2 (Join-Path $PSScriptRoot 'IconIdentitySmoke.cpp') -o (Join-Path $out 'IconIdentitySmoke.exe') -lole32 -luuid
if ($LASTEXITCODE -ne 0) { throw 'Icon identity regression compilation failed' }
& (Join-Path $root 'tools\build-shellbridge.ps1') -Zig $Zig -OutputDirectory $out
[IO.File]::WriteAllBytes((Join-Path $out 'empty.apk'), [byte[]]@())
& (Join-Path $out 'BridgeSmoke.exe') (Join-Path $out 'ApkShellext2.Native.dll') (Join-Path $out 'empty.apk')
if ($LASTEXITCODE -ne 0) { throw 'Native bridge regression failed' }
& (Join-Path $out 'IconIdentitySmoke.exe') (Join-Path $out 'ApkShellext2.Native.dll') (Join-Path $out ('identity-fixture-' + [Guid]::NewGuid().ToString('N')))
if ($LASTEXITCODE -ne 0) { throw 'Icon identity regression failed' }
