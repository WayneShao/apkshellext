# ApkShellext2 v0.5.0 — isolated Shell worker

Opening a package after another Explorer extension loaded .NET 8 caused v0.4's .NET 10 COM host to fail with 0x800080A5. The four Shell handlers now use a native C++ COM DLL. Package parsing, Svg.Skia rendering and the existing menu/settings UI run in a separate .NET 10 worker. Explorer no longer loads ApkShellext2's managed assembly or starts its CLR.

The worker remains on .NET 10; install the x64 .NET 10 Windows Desktop Runtime. This is a native bridge plus managed worker, not Native AOT compilation of the legacy WinForms/WPF project. The bridge invokes an explicitly located sibling executable without a command shell, bounds response sizes and terminates timed-out noninteractive workers. Thumbnail streams use temporary snapshots and release original stream ownership.

The classic context menu opens a package actions window containing the previous actions/preferences. This extra window keeps managed UI outside Explorer. Noninteractive workers exit after their request. There is no service or automatic registry-repair background task.

Run install.bat as administrator to register the native DLL with Apartment threading and repair package icon associations, then restart applications that loaded an older extension. The installer preserves the default application and uses the existing ownership journal for association restoration. check-registration.bat distinguishes a wrong COM path from a static DefaultIcon overwrite by another application. Such future external registry writes cannot be prevented by runtime isolation.

Deploy the complete folder, including ApkShellext2.Native.dll, ApkShellext2.Worker.exe, .NET runtime metadata and native Skia/WebP dependencies. The package deliberately excludes ApkShellext2.comhost.dll. Preserve the previous versioned folder for rollback.

Validation commands: tools/test-isolation.ps1 tests native, .NET 8 and .NET 10 hosts; missing, failed and timed-out workers; real icon/info/thumbnail callbacks; empty download write/rename and host-module isolation. Worker/smoke.py verifies the binary protocol and WorkerTests verifies existing menu construction. Large IStream thumbnail inputs incur a temporary copy; cold worker startup adds latency and requests use an eight-second deadline.
