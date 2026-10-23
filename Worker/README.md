# Isolated package worker

`tools/build-worker.ps1` publishes the framework-dependent .NET 10 Windows worker
and its managed/native dependencies to `artifacts/isolated/worker`. The native
shell bridge DLL and this worker executable must be installed as siblings. The
worker has no COM registration entry point and the publish script removes the
legacy managed COM host and its runtime/configuration files.

Each request starts a new process. Its standard output is exclusively binary;
dependency `Console.WriteLine` output is discarded and failures go to standard
error. A nonzero exit code means the shell must ignore the response.

* `--icon SIZE PATH`: original icon handler, including type/overlay settings.
* `--thumbnail SIZE PATH`: original stream thumbnail handler, reading the native
  bridge's snapshot directly. Disabled thumbnails return nonzero and no bytes.
* Image responses: little-endian `uint32 width`, `uint32 height`, then top-down
  premultiplied BGRA pixels, no padding. Requested sizes are 1–1024 inclusive;
  dimensions never exceed the requested size. Maximum response: 4,194,312 bytes.
* `--info PATH`: original localized info-tip formatter. Response: little-endian
  `uint32 UTF-16 code-unit count`, then UTF-16LE text without BOM or terminator,
  at most 32,768 code units. Failed parsing uses the existing failure text.
* `--menu PATH...`: interactive standalone Windows Forms host containing the
  original menu tree and event handlers, including preferences. Only this command
  starts a UI message loop. Closing the host ends its process.
* `--associations install|uninstall`: installer-only command using the existing
  association ownership journal. It does not register the worker or change COM
  server paths. The installer owns native CLSID registration and rollback.

Verification without registering COM or invoking menu actions:

```powershell
.\tools\build-worker.ps1
python Worker\smoke.py
dotnet run --project WorkerTests\WorkerTests.csproj -c Release -p:EnableComHosting=false
```

The menu construction test temporarily suppresses the existing automatic update
check using `LastCheckUpdateTime` and restores its previous value and registry
kind in a `finally` block. It does not show a window or invoke menu actions.
