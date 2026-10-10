# Audit and validation evidence — 0.2.0.8

Base: kkguo/apkshellext 79b9e4e54c2d04c8fdddcd458305c7796694aa27, branch ApkShellext2. Audit date: 2026-10-10.

## Confirmed failures and remedies

- Installed0.2.0.7 and original source delayed release after empty/missing-entry constructor exceptions. Real registered SHGetFileInfo icon extraction on an empty final .apk reproduced subsequent writer ERROR_SHARING_VIOLATION. All package filename reads now use Read + ReadWrite/Delete sharing, with deterministic owned-stream cleanup on exceptions.
- A zero-sized Android chunk and a bundle without application entries hung separate test processes. Chunk traversal is bounded and must progress; bundle XML iteration ends at EOF.
- Cyclic drawables and deep/resource reference failures needed cycle/depth bounds and finally cleanup.
- Modern compact/sparse/offset16/type-offset tables initially failed resolution; they now resolve equivalent generated names, paths and aliases. Forty-two malformed variations and the sparse maximum-offset distinction are covered.
- Legacy booleans were reversed; entry-zero images and uppercase file extensions failed. Desktop APPX assumed a phone-only node, while deflated nested APPX streams were nonseekable.
- COM stream baseline passed1/16 scenarios; new wrapper passes16/16 including real shared-RCW ownership and1000 create/dispose cycles. Thumbnail base now disposes its managed Bitmap after transferring an independent HBITMAP.
- Native libwebp reported1.0.3. Maintained DLLs are built from official1.6.0 source for both architectures. Old managed decoding flattened alpha to255; new decoder preserves alpha and rejects oversized/truncated inputs.
- Marker layout baseline failed requested footprint. New shared compositor passes6 sizes, marker bounds/alpha/center preservation/ownership checks.
- Optional service had an exclusive readwrite file open, unsynchronized registration, remote path registration and build side effects. Source fixes and26 checks cover these; service is not in release or installed.

## Final source verification

Full Release build succeeds. Ten pre-existing compiler warnings remain in inherited drawing/resource code and third-party SVG source; no build errors.

-11 reader/update scenarios pass.
-19 named parser scenarios pass (including map-layout combinations and42 malformed table mutations).
-16 COM stream scenarios pass.
-2 thumbnail ownership scenarios pass.
-6 overlay sizes pass.
-WebP native version1.6.0, pixel alpha/order and malformed/repeated decode cases pass in both x64 and x86 processes.
-26 service checks pass; no service/listener was started.
-8 recent real APKs pass name/version/icon extraction: television2.2.0.219, ZhenGuoJian0.2.65, HongGuoJian0.2.64, HongGuoTV0.8.0, myDV2.0.31, Nova6.5.5-20261009.2113, SmartTube32.56, TV Bro2.1.6. Local personal APKs are not committed.
-Independent reviews caught repeated whole-document validation and undisposed successful thumbnails; both were corrected and retested.

## Scope boundaries

This is a focused reliability/compatibility audit, not a security certification of all inherited code/dependencies. No browser process crash dump was analyzed, so the downloader-blocking mechanism is proven but a particular browser crash root cause is not. Cross-split resource merging, protobuf/AAB, advanced inline/vector features and full Android resource selection remain outside scope. Native WebP dependencies are maintained; other inherited decoders still require future review.

Use tools/test-installed.ps1 after installation to verify the registered assembly and5 empty final-path icon/write/rename/delete cycles without GC. It fails against the original installed version due to the write-sharing violation.
