#Requires -RunAsAdministrator
param([ValidateSet('Install','Uninstall','Check')][string]$Action = 'Install')
$ErrorActionPreference = 'Stop'
$native = Join-Path $PSScriptRoot 'ApkShellext2.Native.dll'
$worker = Join-Path $PSScriptRoot 'ApkShellext2.Worker.exe'
$ids = @('1f869cee-4fda-35d9-896f-43975a87d1f6','946435a5-fe96-416d-99db-e94ee9fb46c8','dcb629fc-f86f-456f-8e24-98b9b2643a9b','d747c5a7-2f66-4b7d-8301-8531838e4ed3')
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Windows x64 is required.' }
foreach ($file in @($native,$worker)) { if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing payload: $file" } }
$machine = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry64)
$effective = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::ClassesRoot,[Microsoft.Win32.RegistryView]::Registry64)
function Invoke-AssociationWorker([string]$operation) {
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $worker
    $start.Arguments = '--associations ' + $operation
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $p = New-Object Diagnostics.Process
    $p.StartInfo = $start
    try {
        [void]$p.Start()
        $stdout = $p.StandardOutput.ReadToEndAsync()
        $stderr = $p.StandardError.ReadToEndAsync()
        if (-not $p.WaitForExit(30000)) { $p.Kill(); $p.WaitForExit(); throw 'Association registration timed out.' }
        $message = $stderr.GetAwaiter().GetResult()
        [void]$stdout.GetAwaiter().GetResult()
        if ($p.ExitCode -ne 0) { throw "Association registration failed: $message" }
    } finally { $p.Dispose() }
}
function Get-RegistrationSnapshot($Classes, [string[]]$Ids, $LookupClasses = $null) {
    if (-not $LookupClasses) { $LookupClasses = $Classes }
    $targets = @{}
    foreach ($id in $Ids) { $targets["CLSID\{$id}\InprocServer32"] = @('','ThreadingModel','Class','Assembly','RuntimeVersion','CodeBase') }
    foreach ($extension in @('.apk','.ipa','.appx','.appxbundle')) {
        $key = $LookupClasses.OpenSubKey($extension)
        try { $progId = if ($key) { $key.GetValue('', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) } else { $null } }
        finally { if ($key) { $key.Dispose() } }
        $association = $extension
        if ($progId -is [string] -and -not [string]::IsNullOrWhiteSpace($progId) -and $progId.IndexOfAny([char[]]"\/$([char]0)") -lt 0 -and $progId -ne 'CLSID') { $association = $progId }
        foreach ($base in @($association, "SystemFileAssociations\$extension")) {
            foreach ($slot in @('DefaultIcon','ShellEx\IconHandler','ShellEx\ContextMenuHandlers\ApkContextMenu','ShellEx\{00021500-0000-0000-C000-000000000046}')) { $targets["$base\$slot"] = @('') }
            if ($extension -eq '.apk') { $targets["$base\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}"] = @('') }
        }
    }
    $values = @(foreach ($path in $targets.Keys) {
        $key = $Classes.OpenSubKey($path)
        try {
            foreach ($name in $targets[$path]) {
                $exists = $key -and ($key.GetValueNames() -contains $name)
                [pscustomobject]@{ Path=$path; Name=$name; Exists=[bool]$exists; Kind=$(if ($exists) { [int]$key.GetValueKind($name) } else { 1 }); Value=$(if ($exists) { ,$key.GetValue($name,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) } else { $null }) }
            }
        } finally { if ($key) { $key.Dispose() } }
    })
    $journal = @(Get-RegistrationJournal $Classes 'ApkShellext2.Registration')
    [pscustomobject]@{ Values=$values; Journal=$journal }
}
function Get-RegistrationJournal($Classes, [string]$Path) {
    $key = $Classes.OpenSubKey($Path)
    if (-not $key) { return }
    try {
        $values = @(foreach ($name in $key.GetValueNames()) { [pscustomobject]@{ Name=$name; Kind=[int]$key.GetValueKind($name); Value=$key.GetValue($name,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) } })
        [pscustomobject]@{ Path=$Path; Values=$values }
        foreach ($child in $key.GetSubKeyNames()) { Get-RegistrationJournal $Classes "$Path\$child" }
    } finally { $key.Dispose() }
}
function Restore-RegistrationSnapshot($Classes, $Snapshot) {
    foreach ($value in $Snapshot.Values) {
        $key = if ($value.Exists) { $Classes.CreateSubKey($value.Path) } else { $Classes.OpenSubKey($value.Path,$true) }
        if ($key) {
            try {
                if ($value.Exists) { $key.SetValue($value.Name,$value.Value,[Microsoft.Win32.RegistryValueKind]$value.Kind) }
                else { $key.DeleteValue($value.Name,$false) }
            } finally { $key.Dispose() }
        }
    }
    # Only our private journal is replaced; foreign association containers survive.
    $Classes.DeleteSubKeyTree('ApkShellext2.Registration',$false)
    foreach ($entry in $Snapshot.Journal) {
        $key = $Classes.CreateSubKey($entry.Path)
        try { foreach ($value in $entry.Values) { $key.SetValue($value.Name,$value.Value,[Microsoft.Win32.RegistryValueKind]$value.Kind) } }
        finally { $key.Dispose() }
    }
}

$snapshot = $null
$classes = $machine.CreateSubKey("Software\Classes")
$currentUser = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry64)
$userClasses = $currentUser.CreateSubKey("Software\Classes")
try {
    if ($Action -eq 'Install') {
        # Refuse a hidden per-user server override before changing machine state.
        $user = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry64)
        try {
            foreach ($id in $ids) {
                $key = $user.OpenSubKey("Software\Classes\CLSID\{$id}\InprocServer32")
                try { if ($key -and $key.ValueCount -gt 0) { throw "Per-user COM override exists for {$id}; resolve it before installation." } }
                finally { if ($key) { $key.Dispose() } }
            }
        } finally { $user.Dispose() }
        $snapshot = [pscustomobject]@{
            Machine = (Get-RegistrationSnapshot $classes $ids)
            User = (Get-RegistrationSnapshot $userClasses @() $effective)
        }
        $backupDirectory = Join-Path $env:LOCALAPPDATA 'ApkShellext2\RegistrationBackups'
        [void][IO.Directory]::CreateDirectory($backupDirectory)
        $backupPath = Join-Path $backupDirectory ((Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N') + '.clixml')
        $snapshot | Export-Clixml -LiteralPath $backupPath -Depth 20
        Write-Host "Registry rollback snapshot: $backupPath"
        foreach ($id in $ids) {
            $key = $machine.CreateSubKey("Software\Classes\CLSID\{$id}\InprocServer32")
            try {
                $key.SetValue('', $native)
                $key.SetValue('ThreadingModel','Apartment')
                # Values from Framework/RegAsm have no meaning for the native bridge.
                foreach ($name in @('Class','Assembly','RuntimeVersion','CodeBase')) { $key.DeleteValue($name,$false) }
            } finally { $key.Dispose() }
        }
        # Native CLSIDs are ready before the legacy association helper notifies Shell.
        Invoke-AssociationWorker 'install'
    } elseif ($Action -eq 'Uninstall') {
        # A newer installation owns these CLSIDs; never undo its associations.
        foreach ($id in $ids) {
            $key = $machine.OpenSubKey("Software\Classes\CLSID\{$id}\InprocServer32")
            try { if ($key -and $key.GetValue('') -ne $native) { throw "Another installation owns CLSID {$id}; use its uninstaller." } }
            finally { if ($key) { $key.Dispose() } }
        }
        Invoke-AssociationWorker 'uninstall'
        foreach ($id in $ids) {
            $key = $machine.OpenSubKey("Software\Classes\CLSID\{$id}\InprocServer32",$true)
            try { if ($key -and $key.GetValue('') -eq $native) { $key.DeleteValue('',$false); $key.DeleteValue('ThreadingModel',$false) } }
            finally { if ($key) { $key.Dispose() } }
        }
    }
    if ($Action -ne 'Uninstall') {
        foreach ($id in $ids) {
            $key = $effective.OpenSubKey("CLSID\{$id}\InprocServer32")
            try { if (-not $key -or $key.GetValue('') -ne $native -or $key.GetValue('ThreadingModel') -ne 'Apartment') { throw "Effective COM registration differs: {$id}. Check per-user overrides." } }
            finally { if ($key) { $key.Dispose() } }
        }
        foreach ($extension in @('.apk','.ipa','.appx','.appxbundle')) {
            $key = $effective.OpenSubKey($extension)
            try { $progId = if ($key) { $key.GetValue('') } else { $null } }
            finally { if ($key) { $key.Dispose() } }
            $association = if ($progId -is [string] -and $progId -and $progId.IndexOfAny([char[]]'\/') -lt 0) { $progId } else { $extension }
            $icon = $effective.OpenSubKey("$association\DefaultIcon")
            try { if (-not $icon -or $icon.GetValue('') -ne '%1') { throw "Icon association was overwritten: $association\DefaultIcon must be %1. Run Install to repair." } }
            finally { if ($icon) { $icon.Dispose() } }
            $slots = @{
                'ShellEx\IconHandler' = $ids[0]
                'ShellEx\{00021500-0000-0000-C000-000000000046}' = $ids[1]
                'ShellEx\ContextMenuHandlers\ApkContextMenu' = $ids[2]
            }
            if ($extension -eq '.apk') { $slots['ShellEx\{e357fccd-a995-4576-b01f-234630154e96}'] = $ids[3] }
            foreach ($slot in $slots.Keys) {
                $key = $effective.OpenSubKey("$association\$slot")
                try { if (-not $key -or $key.GetValue('') -ne ('{' + $slots[$slot] + '}')) { throw "Shell association differs: $association\$slot" } }
                finally { if ($key) { $key.Dispose() } }
            }
        }
    }
    # Notify only; restarting Explorer is a separate deployment action.
    if (-not ('ApkShellAssociationNotify' -as [type])) {
        Add-Type 'using System; using System.Runtime.InteropServices; public static class ApkShellAssociationNotify { [DllImport("shell32.dll")] public static extern void SHChangeNotify(uint e,uint f,IntPtr a,IntPtr b); }'
    }
    if ($Action -ne 'Check') { [ApkShellAssociationNotify]::SHChangeNotify(0x08000000,0,[IntPtr]::Zero,[IntPtr]::Zero) }
    Write-Host "$Action completed: native Shell bridge, isolated .NET 10 worker."
} catch {
    $installationError = $_
    if ($snapshot) {
        try {
            # Attempt both restores even if one hive has become unwritable.
            $restoreErrors = @()
            try { Restore-RegistrationSnapshot $classes $snapshot.Machine } catch { $restoreErrors += $_ }
            try { Restore-RegistrationSnapshot $userClasses $snapshot.User } catch { $restoreErrors += $_ }
            if ($restoreErrors.Count) { throw ($restoreErrors | Out-String) }
            if (-not ('ApkShellAssociationNotify' -as [type])) {
                Add-Type 'using System; using System.Runtime.InteropServices; public static class ApkShellAssociationNotify { [DllImport("shell32.dll")] public static extern void SHChangeNotify(uint e,uint f,IntPtr a,IntPtr b); }'
            }
            [ApkShellAssociationNotify]::SHChangeNotify(0x08000000,0,[IntPtr]::Zero,[IntPtr]::Zero)
            Write-Warning "Installation failed; prior registry values restored. Backup: $backupPath"
        } catch { throw "Installation failed: $installationError. Registry rollback also failed: $_. Snapshot: $backupPath" }
    }
    throw $installationError
} finally { $userClasses.Dispose(); $currentUser.Dispose(); $classes.Dispose(); $machine.Dispose(); $effective.Dispose() }
