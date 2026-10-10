# Legacy package-sharing service

This auxiliary service is not included in the shell-extension release archive or installed by the shell-extension installer. Building it does not stop, start, register, or install a service. Its original QR-download integration in the extension is inactive.

Package registration accepts only a loopback caller, a 32-character hexadecimal key, and an existing package file with an APK, IPA, APPX, or APPXBUNDLE extension. Directories and final-path reparse points are rejected. Registry reads and writes are synchronized. Download requests remain reachable remotely by their registered key so the original phone/QR use case remains possible.

Loopback is **not user authentication**. Any local process can submit registrations. The historical installer selected LocalSystem, so existing deployments using it evaluate filesystem access with service privileges, not the registering user's privileges. An extension check does not establish file provenance or validate package contents. The caller-supplied key is a lookup token, not an authorization protocol. Do not treat these limited repairs as a multi-user authentication or access-control redesign. Service deployment requires a separate decision about identity, permissions, and intended network exposure.

Transfers open files read-only with `FileShare.ReadWrite | FileShare.Delete`. A fixed 64 KiB buffer copies the length observed when the file is opened; bytes appended later are excluded, and truncation aborts the transfer. Concurrent writes are allowed, so a changing file is not a consistent download snapshot.

Run `powershell -File tools/test-service.ps1` from the repository root for deterministic registration, concurrency, file-sharing, transfer, and prefix tests. They do not start a service or bind a listener port.

## .NET 10 auxiliary build

The SDK-style project targets `net10.0-windows` and uses `System.ServiceProcess.ServiceController` 10.0.12. Build it from the repository root with `dotnet build ApkShellextService/apkShellextService.csproj -c Release`. This is a compilation check; it does not validate a running service or change service registration.

`apkShellextServiceInstaller.cs` remains in source as historical reference but is excluded from compilation. Its .NET Framework `System.Configuration.Install` / InstallUtil mechanism is deprecated for this project and is not supported by the .NET 10 build. No replacement service installer is supplied or included in the shell-extension release. Deployment of this auxiliary service remains a separately scoped operation; the shell-extension installer does not install or start it.
