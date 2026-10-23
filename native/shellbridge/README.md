# Native Shell bridge

`bridge.cpp` supplies the four existing COM class IDs without loading a managed runtime. Build with `tools/build-shellbridge.ps1`; its default output is `artifacts/isolated/shell/ApkShellext2.Native.dll`. Install the real `ApkShellext2.Worker.exe` and its dependencies beside that DLL. Registration is managed separately.

From the repository root, the reproducible build command is:

```powershell
./tools/build-shellbridge.ps1
# Optional explicit toolchain and output:
./tools/build-shellbridge.ps1 -Zig 'D:\Backup\Desktop\apkshellext-analysis\native-build\zig\zig-x86_64-windows-0.15.2\zig.exe' -OutputDirectory './artifacts/isolated/shell'
# Isolated mock-worker regression (does not install or register):
./native/shellbridge/test.ps1
```

The script invokes `zig c++ -target x86_64-windows-gnu -std=c++17 -O2 -shared native/shellbridge/bridge.cpp -o OUTPUT -lole32 -luuid -lshell32 -luser32 -lgdi32`. Do not add `-static`: Zig 0.15.2 combines that flag with `-shared` by emitting an archive rather than a loadable PE DLL. The script checks the result's MZ signature. The inspected DLL imports Windows system/UCRT libraries, with no separate libstdc++, libgcc, CoreCLR, or .NET host DLL dependency.

The native menu uses `安装包操作与设置…` under `APK Shell 扩展` when the user's primary UI language is Chinese; other UI languages use the English labels.

## Worker protocol

The bridge starts the exact sibling executable with `CreateProcessW`, no shell, and correctly quoted arguments:

* `--icon SIZE PATH` and `--thumbnail SIZE PATH`: stdout is little-endian uint32 width, uint32 height, then exactly width × height × 4 bytes of premultiplied BGRA, top-down.
* `--info PATH`: stdout is little-endian uint32 UTF-16 code-unit count then exactly that many UTF-16LE code units, no terminator. Maximum 32768 code units.
* `--menu PATH...`: opens the worker UI. Maximum 128 selections; the complete command line must fit the Windows 32767-character limit.

Image dimensions are 1–1024. All synchronous output is capped at 4 MiB + 8 bytes, and each worker has an 8-second deadline. A suspended child joins a kill-on-close job before it runs; every return path closes the job and kills remaining descendants. A handle inheritance list restricts inherited handles to stdout and NUL. Stderr and stdin use NUL. Pipe polling drains output while the process runs and after exit, avoiding pipe-buffer deadlock.

Thumbnail initialization snapshots the caller's stream immediately without retaining it. Seekable streams are copied from position zero and their original cursor is restored. Non-seekable streams are copied from their current cursor. Snapshot copying has a 512 MiB bound and a 2-second cooperative deadline, with an early Stat size check. A blocked foreign `IStream::Read`, `Stat`, or `Seek` cannot be interrupted safely by this in-process adapter.

The snapshot uses a GUID-named, exclusively created per-user temporary `.apk`, shared for read/write/delete and marked delete-on-close. Worker readers must use `FileShare.ReadWrite | FileShare.Delete`. It is deleted immediately after the first thumbnail worker finishes, including failure and exception paths. If no thumbnail is requested, object destruction deletes it. Later thumbnail requests copy or downscale the cached native bitmap; they do not re-open the original source and never upscale a smaller worker image. A later larger request therefore uses the first rendering's resolution.

Worker failures return an HRESULT so the Shell can choose its own fallback. Empty-file fallback image selection belongs to the worker. Each large/small icon request runs separately to preserve requested rendering quality. There is no global image cache or persistent background worker.

## Isolated protocol regression

`MockWorker.cpp` is a test-only worker: never place it in an installed shell directory. `BridgeSmoke.cpp` tests a 1024 image larger than a pipe buffer, icon handle ownership, UTF-16 infotips, memory-stream snapshot/cursor restoration, truncated and oversized response rejection, the eight-second timeout, COM unloading, and absence of CoreCLR in the host. `test.ps1` builds and runs these under `artifacts/isolated/bridge-tests`.

These protocol tests do not prove Explorer UI integration, real APK rendering, menu GUI interaction, or coexistence with another runtime; the repository's integration tests cover those separately.
