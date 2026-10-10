# Android resource-format compatibility

Audited against current AOSP on 2026-10-10. APK age alone is not a format indicator: build-tool options can select newer encodings, and sparse maps already existed before this project's 2019 release.

## Implemented in the quick APK reader

| Encoding | Decoder behavior | Regression coverage |
| --- | --- | --- |
| Dense 32-bit entry map | Byte offsets; `0xffffffff` is absent | Name, icon path, alias, absent IDs |
| Sparse map (`FLAG_SPARSE=1`) | Sorted `(uint16 ID, uint16 offset/4)` tuples | IDs larger than tuple count; missing ID; malformed order; maximum offset |
| Dense 16-bit map (`FLAG_OFFSET16=2`) | Offsets multiplied by four; `0xffff` is absent | Name, icon path, alias, absent IDs |
| Compact entry (`FLAG_COMPACT=8`) | Key in first word; type in flags' high byte; inline data | All three map layouts, including references |
| Package `typeIdOffset` | Add package offset to local type ID; range-check result | All layouts and both entry representations |
| Extended resource config | Preserve declared config bytes within validated header | Existing density selection retained |

Sparse `0xffff` offsets are valid offsets, unlike the dense offset16 sentinel. Combined sparse+offset16 flags are rejected as contradictory layouts. Complex map/bag entries remain outside simple label/icon resolution.

The decoder follows [AOSP ResourceTypes.h](https://android.googlesource.com/platform/frameworks/base/+/master/libs/androidfw/include/androidfw/ResourceTypes.h) (inspected blob `e330410ed1a0890be266da0e62a3e6d1a25a3cdc`) and checks lookup behavior against [AOSP LoadedArsc.cpp](https://android.googlesource.com/platform/frameworks/base/+/master/libs/androidfw/LoadedArsc.cpp).

## Evidence and boundaries

The four initial modern-format tests failed before implementation: compact dense entries were mistaken for a short header; sparse and offset16 maps were skipped; nonzero package type offsets failed lookup. After implementation, each representation resolves the same generated label, icon path, and reference. The icon path is decoded into a bitmap. Forty-two corrupt-table combinations are rejected, and a separate sparse maximum-offset fixture distinguishes its semantics from offset16. Each named test runs in a separate process with a timeout.

These tests cover binary `resources.arsc` inside a single APK. They do not implement runtime resource merging across base/config/feature APKs, dynamic shared-library package-ID remapping, Android overlay selection, or complete Android resource qualifier matching. A split APK may refer to values present only in another split; adding `typeIdOffset` support alone cannot resolve absent external data. App bundles/protobuf resource tables are separate containers and are not made readable by these binary-table changes.

Real-file checks supplement synthetic coverage; they cannot establish universal Android compatibility. WebP icon decoding additionally requires the matching native `libwebp` DLL in the deployed build. A Nova sample's `res/d2.webp` resolved correctly, but a test folder without that DLL failed image decoding; this is a packaging dependency rather than an entry-map failure.

Run `tools/test-parser.ps1` against the newly built DLL; use `-ApkFixtures` to supply additional local APK paths. Test executables are not release payloads.

## Transparent WebP icons

The shell read path now uses a dedicated BGRA decoder with `Format32bppArgb` and the stable `WebPGetInfo` / `WebPDecodeBGRAInto` APIs, preserving partial and zero alpha. The old managed wrapper's BGR path dropped transparency. Signatures were checked against [libwebp 1.6.0 decode.h](https://raw.githubusercontent.com/webmproject/libwebp/v1.6.0/src/webp/decode.h). The helper rejects invalid dimensions and images over 16,777,216 pixels before bitmap allocation, and releases locked bitmap storage on decode failures.

The generated RGBA fixture first failed on the old wrapper (alpha 255 where 128 was expected). The new path passes exact-alpha/color tests in both x64 and x86 processes using the rebuilt 1.6.0 libraries. Malformed headers, truncated streams, oversized dimensions and repeated corrupt-payload decoding are also tested. With the native libraries packaged, the Nova sample's metadata and WebP icon both pass. These checks cover the static image decoding used for shell icons; they do not add animated-WebP playback.
