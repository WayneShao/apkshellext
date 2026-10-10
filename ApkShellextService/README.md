# Legacy package-sharing service

This auxiliary service is not included in the shell-extension release archive or installed by the shell-extension installer. Building it does not stop, start, register, or install a service. Its original QR-download integration in the extension is inactive.

Package registration accepts only a loopback caller, a 32-character hexadecimal key, and an existing package file with an APK, IPA, APPX, or APPXBUNDLE extension. Directories and final-path reparse points are rejected. Registry reads and writes are synchronized. Download requests remain reachable remotely by their registered key so the original phone/QR use case remains possible.

Loopback is **not user authentication**. Any local process can submit registrations. The legacy installer uses LocalSystem, so filesystem access is evaluated with service privileges, not the registering user's privileges. An extension check does not establish file provenance or validate package contents. The caller-supplied key is a lookup token, not an authorization protocol. Do not treat these limited repairs as a multi-user authentication or access-control redesign. Service deployment requires a separate decision about identity, permissions, and intended network exposure.

Transfers open files read-only with `FileShare.ReadWrite | FileShare.Delete`. A fixed 64 KiB buffer copies the length observed when the file is opened; bytes appended later are excluded, and truncation aborts the transfer. Concurrent writes are allowed, so a changing file is not a consistent download snapshot.

Run `powershell -File tools/test-service.ps1` from the repository root for deterministic registration, concurrency, file-sharing, transfer, and prefix tests. They do not start a service or bind a listener port.
