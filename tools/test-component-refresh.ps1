$ErrorActionPreference='Stop'
$dir=Join-Path $PSScriptRoot ('..\tests\bin\ComponentRefresh-'+[guid]::NewGuid().ToString('N'))
dotnet build (Join-Path $PSScriptRoot '..\tests\Regression.csproj') -c Release -r win-x64 -p:Suite=ComponentRefresh -o $dir "-p:BaseIntermediateOutputPath=$dir\obj\" --nologo
if($LASTEXITCODE -ne 0){throw 'Component refresh test build failed'}
Get-ChildItem (Join-Path $PSScriptRoot "..\artifacts\build\win-x64") -File | Where-Object { $_.Name -in @("SkiaSharp.dll","Svg.Skia.dll","Svg.Model.dll","Svg.Custom.dll","Svg.Animation.dll","Svg.SceneGraph.dll","HarfBuzzSharp.dll","ShimSkiaSharp.dll","libSkiaSharp.dll","libHarfBuzzSharp.dll","ExCSS.dll") } | Copy-Item -Destination $dir
& dotnet (Join-Path $dir 'ComponentRefresh.dll')
if($LASTEXITCODE -ne 0){throw 'Component refresh test failed'}
