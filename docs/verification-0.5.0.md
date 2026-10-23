# v0.5 isolation verification

2026-10-23. Installed version 0.5.0.0. The v0.4 .NET 10 COM host previously failed with 0x800080A5 after .NET 8 had already loaded. Current native bridge tests keep the original host runtime unchanged.

- Native bridge SHA256: 7c9cdbe733066b998344aa64ed2aa950ebcdd89b54e28b6f12da54d9ef0e9d71.
- No-runtime, .NET 8.0.31 and .NET 10.0.12 hosts: real icon64/32, information, stream-thumbnail and initialized context-menu callbacks pass. Host module and managed-assembly pollution checks pass.
- Registered-mode tests additionally use SHGetFileInfo on an empty APK followed by write/rename, and verify the exact registered native DLL path.
- Missing worker, abnormal worker exit and timeout fixtures pass. Timeout observed near8.1seconds. Original input APK hash unchanged; no noninteractive worker survived completion.
- Thumbnail enabled/disabled settings tested and restored; worker protocol PArgb/size checks pass. Context-menu construction covers single corrupt input and multiselection, retaining preferences. GUI action clicks were not automated.
- Reader regressions11/11, parser regressions19synthetic plus a real DownloadProvider APK pass. Two-hive association tests35checks pass. Snapshot rollback tests preserve typed values and default app.
- Actual installation initially detected an HKCU static-icon override and rolled back. Registration was corrected to maintain independent machine/current-user ownership journals. Subsequent install and Check pass; Androws remains the default APK opener.
- Actual Explorer process loads ApkShellext2.Native.dll only for this extension, with no ApkShellext2 managed DLL or Skia module. .NET8 coexistence was directly proven in controlled registered hosts; RaiDrive was not observed loaded in the freshly restarted Explorer during final inspection.
- Installed payload64files matches package manifest. Source-file and native helper timeouts remain distinct: a foreign blocking IStream call cannot be forcibly interrupted safely inside Explorer. The current bridge uses a bounded temporary stream snapshot and cooperative copy deadline.

Full native/mock, isolation and worker test commands are in native/shellbridge/README.md, tools/test-isolation.ps1 and Worker/README.md. This validates the runtime-conflict fix, not arbitrary future third-party registry edits or long-duration Explorer stress.
