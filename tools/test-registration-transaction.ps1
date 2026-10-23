$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'register-isolated.ps1'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($script,[ref]$tokens,[ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
# Import only pure snapshot helpers; never execute installer top-level statements.
foreach ($name in @('Get-RegistrationSnapshot','Get-RegistrationJournal','Restore-RegistrationSnapshot')) {
    $definition = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name },$true)
    if (-not $definition) { throw "Missing helper: $name" }
    Invoke-Expression $definition.Extent.Text
}
$fixturePath = 'Software\ApkShellext2.Tests\Transaction-' + [Guid]::NewGuid().ToString('N')
$root = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($fixturePath)
$id = '1f869cee-4fda-35d9-896f-43975a87d1f6'
function Set-TestValue($path,$name,$value,$kind = [Microsoft.Win32.RegistryValueKind]::String) {
    $key = $root.CreateSubKey($path)
    try { $key.SetValue($name,$value,$kind) } finally { $key.Dispose() }
}
function Read-TestValue($path,$name) {
    $key = $root.OpenSubKey($path)
    try { if ($key) { $key.GetValue($name,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) } } finally { if ($key) { $key.Dispose() } }
}
try {
    Set-TestValue '.apk' '' 'Foreign.Package'
    Set-TestValue 'Foreign.Package\DefaultIcon' '' '%TEMP%\old.ico' ([Microsoft.Win32.RegistryValueKind]::ExpandString)
    Set-TestValue 'Foreign.Package\ShellEx\IconHandler' '' 'foreign-handler'
    Set-TestValue 'Foreign.Package\Unrelated' 'Keep' ([byte[]](1,2,3)) ([Microsoft.Win32.RegistryValueKind]::Binary)
    Set-TestValue "CLSID\{$id}\InprocServer32" '' 'old.comhost.dll'
    Set-TestValue "CLSID\{$id}\InprocServer32" 'RuntimeVersion' 'v4.0.30319'
    Set-TestValue 'ApkShellext2.Registration\old\record' 'Previous' ([long]1234567890123) ([Microsoft.Win32.RegistryValueKind]::QWord)
    $snapshot = Get-RegistrationSnapshot $root @($id)
    # Round trip persisted backup as well as live rollback data types.
    $serialized = [Management.Automation.PSSerializer]::Serialize($snapshot,20)
    $snapshot = [Management.Automation.PSSerializer]::Deserialize($serialized)
    try {
        Set-TestValue "CLSID\{$id}\InprocServer32" '' 'new-native.dll'
        Set-TestValue "CLSID\{$id}\InprocServer32" 'ThreadingModel' 'Apartment'
        Set-TestValue 'Foreign.Package\DefaultIcon' '' '%1'
        Set-TestValue 'Foreign.Package\ShellEx\IconHandler' '' 'our-handler'
        Set-TestValue 'SystemFileAssociations\.apk\ShellEx\IconHandler' '' 'our-handler'
        Set-TestValue 'ApkShellext2.Registration\new\record' 'Owned' 'new'
        throw 'Simulated failure after partial association write'
    } catch { Restore-RegistrationSnapshot $root $snapshot }
    if ((Read-TestValue '.apk' '') -ne 'Foreign.Package') { throw 'Default application changed' }
    if ((Read-TestValue 'Foreign.Package\DefaultIcon' '') -ne '%TEMP%\old.ico') { throw 'DefaultIcon not restored' }
    $key = $root.OpenSubKey('Foreign.Package\DefaultIcon'); try { if ($key.GetValueKind('') -ne 'ExpandString') { throw 'Registry kind lost' } } finally { $key.Dispose() }
    if ((Read-TestValue 'Foreign.Package\ShellEx\IconHandler' '') -ne 'foreign-handler') { throw 'Foreign handler not restored' }
    if ((Read-TestValue "CLSID\{$id}\InprocServer32" '') -ne 'old.comhost.dll') { throw 'CLSID not restored' }
    if ($null -ne (Read-TestValue "CLSID\{$id}\InprocServer32" 'ThreadingModel')) { throw 'New value not removed' }
    if ($null -ne (Read-TestValue 'SystemFileAssociations\.apk\ShellEx\IconHandler' '')) { throw 'New fallback not removed' }
    if ((Read-TestValue 'ApkShellext2.Registration\old\record' 'Previous') -ne [long]1234567890123) { throw 'Private journal not restored' }
    if ($null -ne (Read-TestValue 'ApkShellext2.Registration\new\record' 'Owned')) { throw 'New journal record remains' }
    if ((Read-TestValue 'Foreign.Package\Unrelated' 'Keep').Length -ne 3) { throw 'Foreign data removed' }
    $user = $root.CreateSubKey('UserClasses')
    $lookup = $root.CreateSubKey('EffectiveClasses')
    try {
        $key = $lookup.CreateSubKey('.apk'); try { $key.SetValue('', 'Androws.apk') } finally { $key.Dispose() }
        $key = $user.CreateSubKey('Androws.apk\DefaultIcon'); try { $key.SetValue('', '%LOCALAPPDATA%\Tencent\apk.ico', [Microsoft.Win32.RegistryValueKind]::ExpandString) } finally { $key.Dispose() }
        $userSnapshot = Get-RegistrationSnapshot $user @() $lookup
        $combined = [pscustomobject]@{ Machine=(Get-RegistrationSnapshot $root @($id)); User=$userSnapshot }
        $combined = [Management.Automation.PSSerializer]::Deserialize([Management.Automation.PSSerializer]::Serialize($combined,20))
        try {
            Set-TestValue "CLSID\{$id}\InprocServer32" '' 'new.dll'
            $key = $user.CreateSubKey('Androws.apk\DefaultIcon'); try { $key.SetValue('', '%1') } finally { $key.Dispose() }
            $key = $user.CreateSubKey('Androws.apk\ShellEx\IconHandler'); try { $key.SetValue('', 'new-handler') } finally { $key.Dispose() }
            throw 'Simulated failure across both hives'
        } catch {
            Restore-RegistrationSnapshot $root $combined.Machine
            Restore-RegistrationSnapshot $user $combined.User
        }
        $key = $user.OpenSubKey('Androws.apk\DefaultIcon')
        try { if ($key.GetValue('', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) -ne '%LOCALAPPDATA%\Tencent\apk.ico' -or $key.GetValueKind('') -ne 'ExpandString') { throw 'User static icon/type not restored' } } finally { $key.Dispose() }
        $key = $user.OpenSubKey('Androws.apk\ShellEx\IconHandler')
        try { if ($key.GetValue('') -ne $null) { throw 'User new handler not removed' } } finally { $key.Dispose() }
        if ((Read-TestValue "CLSID\{$id}\InprocServer32" '') -ne 'old.comhost.dll') { throw 'Two-hive machine restore failed' }
        Write-Output 'PASS two-hive rollback with effective ProgID lookup and typed HKCU static icon restoration'
    } finally { $user.Dispose(); $lookup.Dispose() }
    Write-Output 'PASS installer rollback: CLSID, typed prior defaults, handler, private journal, new fallback removal, foreign ProgID/application preserved'
} finally {
    $root.Dispose()
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($fixturePath,$false)
}
