# Isolated Shell runtime implementation plan

Goal: package Shell handlers must work in Explorer and other hosts whether no CoreCLR, .NET 8, or .NET 10 was loaded first.

Approved architecture: a native C++ COM DLL preserves the four handler CLSIDs. A sibling .NET 10 executable performs package parsing, rendering, existing menu actions and settings. Shell hosts never load the managed extension or Skia. Native AOT is an alternative for a rewritten native-facing layer, not a supported switch on the current built-in COM/WinForms/WPF assembly.

Baseline: existing v0.4 CoCreateInstance succeeds in .NET 10 but fails with 0x800080A5 in .NET 8. Explorer has RaiDrive's .NET 8.0.31 and our comhost without our managed assembly. Also DefaultIcon was overwritten with Androws' static icon; restore %1 explicitly and diagnose separately from runtime activation.

Protocol: one hidden child per bounded request, inherited anonymous stdout pipe, explicit executable path and quoted arguments without shell. Image responses are width/height uint32 LE followed by premultiplied BGRA; info responses are character count uint32 LE followed by UTF16LE. Child job timeout and output size limits prevent indefinite waits. Thumbnail IStream input gets a private temporary snapshot with bounded size and deterministic cleanup. Native handles belong to Shell after callback success. GUI operations run only after an explicit context-menu click.

- [x] native/shellbridge: COM classes, ownership, no runtime hosting, bounded worker transport, fallback on failure.
- [x] Worker: reuse existing rendering/settings/menu logic, exact binary protocol, .NET10 isolation, shared read/write/delete source access.
- [x] tests/Isolation: fresh/native, net8 and net10 host callbacks; failed worker, empty download write/rename, real metadata, alpha, no managed modules in host.
- [x] tools/register-isolated: direct Apartment native COM registration, preserve associations/default application; backup and rollback; no legacy RegAsm entry point.
- [x] build and package: native+worker exact verified payload, runtime/native dependency/license notices, version0.5.0.
- [x] deploy side by side, preserve v0.4, restart Explorer, verify NET8-host coexistence and native-only Explorer modules, refresh thumbnail cache.
- [ ] push final fixes and release verified build; record limitations and evidence.

Acceptance: not merely registry or a .NET10-host test. Actual callbacks must succeed inside a host already running .NET8 without loading our managed DLL; source paths remain writable/deletable after callbacks. Installation must report association drift rather than silently claiming that any future third-party DefaultIcon overwrite is prevented. No background registry rewriting or modification to RaiDrive is planned.
