param([string]$BuildDirectory = "$PSScriptRoot\..\artifacts\build\win-x64")
$ErrorActionPreference = 'Stop'
[xml]$project = Get-Content "$PSScriptRoot\..\ApkShellext2\ApkShellext2.csproj"
if ($project.Project.PropertyGroup.TargetFramework -notcontains 'net10.0-windows') { throw 'Production target must be net10.0-windows' }
foreach ($name in @('ApkShellext2.dll','ApkShellext2.comhost.dll','ApkShellext2.deps.json','ApkShellext2.runtimeconfig.json','libwebp_x64.dll','libwebp_x86.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $BuildDirectory $name))) { throw "Missing modern runtime payload: $name" }
}
$config = Get-Content (Join-Path $BuildDirectory 'ApkShellext2.runtimeconfig.json') -Raw | ConvertFrom-Json
$frameworks = @($config.runtimeOptions.frameworks) + @($config.runtimeOptions.framework)
if (-not ($frameworks | Where-Object { $_.name -eq 'Microsoft.WindowsDesktop.App' -and $_.version -like '10.*' })) { throw 'COM host must request .NET 10 Desktop Runtime' }
Write-Output 'PASS .NET 10 target, native COM host, runtime metadata and native dependencies'
