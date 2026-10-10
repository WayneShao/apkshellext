# ApkShellext2 v0.3.0 — .NET 10 LTS

v0.3 moves the four Windows Shell handlers from .NET Framework 4.5 to a framework-dependent .NET 10 Windows COM host. It requires the x64 Microsoft Windows Desktop Runtime 10.0.x. .NET COM hosting generates `ApkShellext2.comhost.dll`; install/uninstall uses native `System32\regsvr32.exe` and does not use RegAsm for the new host.

The previous v0.2 installation must be unregistered before the v0.3 comhost is registered because the four handler CLSIDs are intentionally preserved. Keep the v0.2.0.8 rollback directory. Existing associations and HKCU settings are preserved by the bounded registration helper.

## Build and test

```powershell
./tools/build.ps1
./tools/test-migration.ps1
./tools/test-registration.ps1
./tools/test-readers.ps1
./tools/test-parser.ps1
./tools/test-com-stream.ps1
./tools/test-thumbnail.ps1
./tools/test-overlay.ps1
./tools/test-webp.ps1
./tools/test-png-migration.ps1
```

The build emits the managed DLL, `.comhost.dll`, `.deps.json`, `.runtimeconfig.json`, managed dependencies, resource satellites and WebP libraries under `artifacts/build/win-x64`. The COM host is x64 because Windows Explorer deployment is x64; the managed assembly is AnyCPU for isolated parser and decoder tests.

v0.3 registers code pages explicitly for ZIP filenames, replaces Ionic.Zlib PNG processing with .NET 10 BCL compression and `System.IO.Hashing`, and fixes .NET 10 assembly-path and browser URL behavior. Build warnings from inherited SharpZipLib/SVG code remain tracked; compilation has no errors.

Modern COM hosting is framework-dependent and cannot be self-contained. Deploy the complete output folder so the comhost, runtime metadata and dependencies stay together.
