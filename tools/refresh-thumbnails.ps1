param([Parameter(Mandatory=$true)][string[]]$Path)
$ErrorActionPreference = 'Stop'
$run = Join-Path $env:TEMP ('ApkShellThumbnailRefresh-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $run | Out-Null
$exe = Join-Path $run 'ThumbnailRefresh.exe'
$framework = if ([Environment]::Is64BitOperatingSystem) { 'Framework64' } else { 'Framework' }
$platform = if ([Environment]::Is64BitOperatingSystem) { 'x64' } else { 'x86' }
& "$env:WINDIR\Microsoft.NET\$framework\v4.0.30319\csc.exe" /nologo /platform:$platform /r:System.Drawing.dll /out:$exe "$PSScriptRoot\ThumbnailRefresh.cs"
if ($LASTEXITCODE -ne 0) { throw 'Thumbnail refresh helper compilation failed' }
$files = foreach ($item in $Path) {
    $resolved = Get-Item -LiteralPath $item
    if ($resolved.PSIsContainer) { Get-ChildItem -LiteralPath $resolved.FullName -File }
    else { $resolved }
}
$failed = 0
foreach ($file in $files | Sort-Object FullName -Unique) {
    if ($file.Extension.ToLowerInvariant() -notin '.apk','.ipa','.appx','.appxbundle' -or $file.Length -eq 0) { continue }
    $process = Start-Process $exe -ArgumentList ('"' + $file.FullName + '"') -PassThru -NoNewWindow
    if (-not $process.WaitForExit(20000)) { $process.Kill(); $process.WaitForExit(); $failed++; Write-Warning "Timed out: $($file.Name)" }
    elseif ($process.ExitCode -ne 0) { $failed++; Write-Warning "Failed: $($file.Name)" }
    else { Write-Output "Refreshed $($file.FullName)" }
}
if ($failed) { throw "$failed thumbnail refreshes failed" }
