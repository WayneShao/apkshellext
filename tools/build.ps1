param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path $repo 'artifacts\build\win-x64'
if (Test-Path -LiteralPath $out) {
    $resolved = (Resolve-Path -LiteralPath $out).Path
    $expected = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts\build\win-x64'))
    if ($resolved -ne $expected -or (Get-Item -LiteralPath $resolved).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected build output directory' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
dotnet build "$repo\ApkShellext2\ApkShellext2.csproj" -c $Configuration -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
foreach ($name in @('libwebp_x64.dll','libwebp_x86.dll')) { Copy-Item "$repo\native\libwebp\$name" $out -Force }
