param([string]$AssemblyPath = "$PSScriptRoot\..\ApkShellext2\bin\Release\ApkShellext2.dll", [int]$TimeoutSeconds=5, [string[]]$ApkFixtures=@())
$ErrorActionPreference='Stop'
$assembly=(Resolve-Path $AssemblyPath).Path
$dir=Split-Path $assembly
$exe=Join-Path $dir 'ParserRegression.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /out:$exe /r:$assembly /r:System.Drawing.dll /r:System.IO.Compression.dll "$PSScriptRoot\..\tests\ParserRegression.cs"
if($LASTEXITCODE -ne 0){ throw 'Test compilation failed' }
$fail=0
foreach($case in @('boolean','xml','manifest','strings','resmap','fixture','sharing','ownership','bounds','cycle','resource_failure','staged_download','legacy_type','modern_dense','modern_sparse','modern_offset16','modern_type_offset','modern_errors','modern_sparse_max')) {
 $p=Start-Process -FilePath $exe -ArgumentList $case -PassThru -NoNewWindow
 if(-not $p.WaitForExit($TimeoutSeconds*1000)){ $p.Kill(); $p.WaitForExit(); Write-Host "FAIL $case timeout"; $fail++ }
 elseif($p.ExitCode -ne 0){$fail++}
}
foreach($apk in $ApkFixtures) {
 $p=Start-Process -FilePath $exe -ArgumentList @('real', ('"'+$apk+'"')) -PassThru -NoNewWindow
 if(-not $p.WaitForExit($TimeoutSeconds*1000)){$p.Kill();$p.WaitForExit();$fail++;Write-Host "FAIL real timeout $apk"} elseif($p.ExitCode -ne 0){$fail++}
}
if($fail){throw "$fail parser regressions failed"}
