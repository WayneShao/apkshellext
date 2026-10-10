param([string]$AssemblyPath = "$PSScriptRoot\..\ApkShellext2\bin\Release\ApkShellext2.dll",[switch]$Baseline,[int]$TimeoutSeconds=15)
$ErrorActionPreference='Stop'
$assembly=(Resolve-Path $AssemblyPath).Path
$fixture=(Resolve-Path "$PSScriptRoot\..\tests\fixtures\alpha-lossless.webp").Path
$failed=0
foreach($arch in @('x64','x86')) {
 $exe=Join-Path (Split-Path $assembly) "WebPRegression.$arch.exe"
 & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:$arch /out:$exe /r:$assembly /r:System.Drawing.dll "$PSScriptRoot\..\tests\WebPRegression.cs"
 if($LASTEXITCODE -ne 0){throw 'WebP test compilation failed'}
 $arguments='"'+$fixture+'"';if($Baseline){$arguments+=' baseline'}
 $process=Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -NoNewWindow
 if(-not $process.WaitForExit($TimeoutSeconds*1000)){$process.Kill();$process.WaitForExit();Write-Host "FAIL WebP $arch timeout";$failed++}elseif($process.ExitCode -ne 0){$failed++}
}
if($failed){throw "$failed WebP regression processes failed"}
