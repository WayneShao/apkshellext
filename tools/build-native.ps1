param([Parameter(Mandatory=$true)][string]$ZigPath)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repo 'tools\bin\libwebp-source'
$commit = '4fa21912338357f89e4fd51cf2368325b59e9bd9'
if (-not (Test-Path -LiteralPath $source)) {
    git clone --depth 1 --branch v1.6.0 https://github.com/webmproject/libwebp.git $source
    if ($LASTEXITCODE -ne 0) { throw 'libwebp checkout failed' }
}
if ((git -C $source rev-parse HEAD).Trim() -ne $commit) { throw 'Unexpected libwebp source commit' }
if ((& $ZigPath version).Trim() -ne '0.15.2') { throw 'This build recipe uses Zig 0.15.2' }
$destination = Join-Path $repo 'native\libwebp'
New-Item -ItemType Directory -Force $destination | Out-Null
$sources = @('src\dec','src\dsp','src\utils','src\enc','sharpyuv') | ForEach-Object {
    Get-ChildItem (Join-Path $source $_) -Filter '*.c' | Sort-Object Name | Select-Object -ExpandProperty FullName
}
foreach ($architecture in @('x64','x86')) {
    $target = if ($architecture -eq 'x64') { 'x86_64-windows-gnu' } else { 'x86-windows-gnu' }
    & $ZigPath cc -shared -O2 -target $target -DWEBP_DLL -DWEBP_USE_THREAD "-I$source" "-I$source\src" "-I$source\sharpyuv" @sources -o "$destination\libwebp_$architecture.dll"
    if ($LASTEXITCODE -ne 0) { throw "Native build failed: $architecture" }
}
Copy-Item "$source\COPYING","$source\PATENTS" $destination
