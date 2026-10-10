# ApkShellext2 0.2.0.8 — download-safe package readers

This maintenance fork is based on kkguo/apkshellext, with original attribution and the four shell-handler identities retained.

## Changes

- Package readers open files read-only with shared read/write/delete access. Viewing an APK must not block a downloader writing its final filename or replacing it.
- Release file/stream resources immediately when parsing an empty, incomplete or malformed APK, IPA, APPX or APPXBundle fails; no forced garbage collection is used.
- Reject invalid Android chunk lengths/offsets and cyclic/deep drawable/resource references instead of looping or overflowing the process stack.
- Support AAPT2 sparse and 16-bit offset tables, compact entries and package type-ID offsets, with bounds-checked decoding.
- Preserve WebP alpha for modern adaptive foreground icons. Ship libwebp 1.6.0 built from pinned official source for x64/x86, replacing 1.0.3.
- Make all APK/IPA/APPX/APPXBundle corner badges 28% of the icon edge, 60% opaque, with 4% margins.
- Repair COM stream short reads, offsets, seeks and repeated disposal.
- Correct Android boolean values, case-insensitive package extensions and images at ZIP entry index zero.
- Terminate APPXBundle scanning at end of manifest, support compressed nested APPX packages and desktop APPX without optional phone identity.
- Bound metadata/nested package extraction and dispose per-read drawing/stream resources.
- Make update checks work on a fresh profile, validate version text, use bounded network timeouts and dispose responses. Updates refer to this fork.

## Install / upgrade

Keep a copy of the previous complete installation directory. Run its uninstall.bat as administrator first. Exit/restart the applications that loaded the old DLL before replacing files (Explorer, Everything or other file managers). Extract this archive to a permanent directory, then run install.bat as administrator. Registration scripts do not terminate applications automatically. Retain existing HKCU settings.

This build is unsigned: the upstream private strong-name PFX is not distributed. Assembly version is 0.2.0.8; the four shell-handler COM GUIDs are unchanged. Always unregister the previous assembly before registering this one. To roll back, unregister this assembly, restore the backup and register the old assembly.

## Build / test

Windows, Visual Studio .NET desktop build tools and .NET Framework runtime are required. Clone recursively, then run:

```powershell
./tools/build.ps1
./tools/test-reliability.ps1
./tools/test-parser.ps1
./tools/test-com-stream.ps1
./tools/test-thumbnail.ps1
./tools/test-overlay.ps1
./tools/test-webp.ps1
./tools/test-service.ps1
./tools/package.ps1
```

Build fetches the pinned Microsoft .NET Framework 4.5 reference-assembly package. No automatic registration occurs during build. The package contains satellite translations and maintained WebP 1.6.0 native dependencies, with their license/patent notices, not test executables.

## Limits

These changes address demonstrated reliability failures; they are not a comprehensive security certification of the old parser or bundled dependencies. AAB/protobuf containers, automatic base/config split merging, dynamic shared-library remapping, inline adaptive drawables and some advanced vector features remain outside this parser's support. See docs/android-format-support.md in the source repository. Metadata is bounded to 16 MiB, resource entries and WebP/IPA/APPX image entry bytes to 128 MiB, nested APPX packages to 512 MiB; unsupported inputs fall back rather than hold a file open. The shell owns streams it passes into thumbnail callbacks, so caller-selected sharing flags cannot be changed retroactively; callbacks release the stream promptly. A writer that itself denies shared reads may temporarily suppress an icon, but the extension must not block that writer.

The optional legacy HTTP service has separate source fixes and tests but is not shipped, enabled or installed by this release. Loopback registration is not a complete multi-user authorization mechanism. See ApkShellextService/README.md.

Raster formats other than WebP still rely on the operating system image decoder and do not have an equivalent decoded-pixel cap. This audit is bounded, not a promise of complete handling of every crafted image.
