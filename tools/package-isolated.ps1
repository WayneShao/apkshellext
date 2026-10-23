param([string]$Version = '0.5.0')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$worker = Join-Path $repo 'artifacts\isolated\worker'
$shell = Join-Path $repo 'artifacts\isolated\shell\ApkShellext2.Native.dll'
$stage = Join-Path $repo ('artifacts\isolated\package-' + [guid]::NewGuid().ToString('N'))
$actual = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $worker 'ApkShellext2.dll')).Version.ToString()
if ($actual -ne "$Version.0") { throw "Expected $Version.0, got $actual" }
New-Item -ItemType Directory -Force $stage | Out-Null
Get-ChildItem -LiteralPath $worker -File | Where-Object { $_.Extension -in '.dll','.exe','.json' } | Copy-Item -Destination $stage
Get-ChildItem -LiteralPath $worker -Directory | ForEach-Object {
    if (Test-Path -LiteralPath (Join-Path $_.FullName 'ApkShellext2.resources.dll')) { Copy-Item -LiteralPath $_.FullName -Destination $stage -Recurse }
}
Copy-Item -LiteralPath $shell -Destination $stage
if (Test-Path "$stage\ApkShellext2.comhost.dll") { throw 'Managed COM host must not be shipped.' }
foreach ($file in @('ApkShellext2.Native.dll','ApkShellext2.Worker.exe','ApkShellext2.Worker.runtimeconfig.json','libSkiaSharp.dll','libHarfBuzzSharp.dll','libwebp_x64.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $stage $file))) { throw "Missing required payload: $file" }
}
New-Item -ItemType Directory "$stage\licenses\libwebp" -Force | Out-Null
Copy-Item "$repo\native\libwebp\COPYING","$repo\native\libwebp\PATENTS","$repo\native\libwebp\README.md" "$stage\licenses\libwebp"
foreach ($notice in @('SharpShell\LICENSE.md','PNGDecrush\LICENSE','QRCoder\LICENSE.txt','WebP-wrapper\LICENSE')) {
    $component = $notice.Split('\')[0]
    New-Item -ItemType Directory "$stage\licenses\$component" -Force | Out-Null
    Copy-Item "$repo\ApkShellext2\$notice" "$stage\licenses\$component"
}
Copy-Item "$repo\docs\licenses\*" "$stage\licenses"
Copy-Item "$repo\docs\THIRD-PARTY-NOTICES.md" "$stage\licenses"
Copy-Item "$repo\ApkShellext2\packages.lock.json" "$stage\licenses\managed-dependencies.lock.json"
Copy-Item "$repo\LICENSE" $stage
Copy-Item "$repo\docs\release-$Version.md" "$stage\RELEASE-NOTES.md"
Copy-Item "$PSScriptRoot\register-isolated.ps1" "$stage\register-extension.ps1"
foreach ($action in @('Install','Uninstall','Check')) {
    $name = if ($action -eq 'Check') { 'check-registration' } else { $action.ToLowerInvariant() }
    '@echo off', ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0register-extension.ps1" -Action ' + $action), 'if errorlevel 1 exit /b 1' | Set-Content "$stage\$name.bat" -Encoding ASCII
}
Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.FullName.Substring($stage.Length + 1).Replace('\','/'))"
} | Set-Content "$stage\SHA256SUMS.txt" -Encoding ASCII
$zip = "$stage.zip"
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Write-Output $stage
