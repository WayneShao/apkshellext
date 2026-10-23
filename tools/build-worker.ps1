param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path $repo 'artifacts\isolated\worker'
dotnet publish "$repo\Worker\ApkShellext2.Worker.csproj" -c $Configuration -r win-x64 --self-contained false -p:EnableComHosting=false -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw "Worker publish failed: $LASTEXITCODE" }
foreach ($name in @('libwebp_x64.dll', 'libwebp_x86.dll')) {
    Copy-Item -LiteralPath "$repo\native\libwebp\$name" -Destination $out -Force
}
# Never package an alternative in-process managed COM entry point with this worker.
foreach ($name in @('ApkShellext2.comhost.dll', 'ApkShellext2.runtimeconfig.json', 'ApkShellext2.dll.config')) {
    $path = Join-Path $out $name
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}
Write-Output "Worker published: $out"
