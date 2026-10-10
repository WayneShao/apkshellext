param([string]$Version = '0.3.0')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$built = Join-Path $repo 'artifacts\build\win-x64'
$actual = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $built 'ApkShellext2.dll')).Version.ToString(); if ($actual -ne '0.3.0.0') { throw "Expected assembly 0.3.0.0, built $actual" }
if ($actual -ne ($Version + '.0')) { throw "Expected $Version, built $actual" }
$dest = Join-Path $repo "artifacts\ApkShellext2-$Version"
if (Test-Path -LiteralPath $dest) { throw "Output already exists: $dest" }
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item "$built\*" $dest -Recurse -Force
Copy-Item "$repo\native\libwebp\libwebp_x64.dll","$repo\native\libwebp\libwebp_x86.dll" $dest
New-Item -ItemType Directory "$dest\licenses\libwebp" -Force | Out-Null
Copy-Item "$repo\native\libwebp\COPYING","$repo\native\libwebp\PATENTS","$repo\native\libwebp\README.md" "$dest\licenses\libwebp\"
Copy-Item "$repo\LICENSE" $dest
Copy-Item "$repo\docs\release-$Version.md" "$dest\RELEASE-NOTES.md"
Copy-Item "$PSScriptRoot\register-extension.ps1" $dest
'@echo off','powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0register-extension.ps1" -Action Install','if errorlevel 1 exit /b 1' | Set-Content "$dest\install.bat" -Encoding ASCII
'@echo off','powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0register-extension.ps1" -Action Uninstall','if errorlevel 1 exit /b 1' | Set-Content "$dest\uninstall.bat" -Encoding ASCII
Get-ChildItem $dest -File -Recurse | Sort-Object FullName | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.FullName.Substring($dest.Length + 1).Replace('\','/'))"
} | Set-Content "$dest\SHA256SUMS.txt" -Encoding ASCII
$archive = "$dest.zip"
Compress-Archive -Path "$dest\*" -DestinationPath $archive
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $archive -Leaf)" | Set-Content "$archive.sha256" -Encoding ASCII
Write-Output $archive

