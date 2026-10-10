param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Install Visual Studio Build Tools with .NET desktop build tools.' }
$cache = Join-Path $repo 'tools\bin\reference-net45'
$references = Join-Path $cache 'package\build\.NETFramework\v4.5'
if (-not (Test-Path (Join-Path $references 'mscorlib.dll'))) {
    New-Item -ItemType Directory -Force $cache | Out-Null
    $archive = Join-Path $cache 'reference.zip'
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net45/1.0.3/microsoft.netframework.referenceassemblies.net45.1.0.3.nupkg' -OutFile $archive
    Expand-Archive $archive (Join-Path $cache 'package') -Force
}
& $msbuild "$repo\ApkShellext2\ApkShellext2.csproj" /t:Rebuild "/p:Configuration=$Configuration" /p:SignAssembly=false /p:PostBuildEvent= /p:RegisterForComInterop=false "/p:FrameworkPathOverride=$references" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Copy-Item "$repo\native\libwebp\libwebp_x64.dll","$repo\native\libwebp\libwebp_x86.dll" "$repo\ApkShellext2\bin\$Configuration\"
# Upstream's private PFX is unavailable. COM GUIDs are retained; install by
# unregistering the previous assembly first. No COM registration occurs here.
