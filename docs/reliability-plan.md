# Reliability audit and repair plan

Authorized scope: audit the existing extension, repair demonstrated defects, fork and push to WayneShao, publish a versioned release, then back up/unregister/replace/register the installed extension and verify it locally.

Preserve shell COM GUIDs and existing user settings. Keep existing parsing/rendering architecture; reject malformed inputs promptly and release owned resources on both success and failure. Do not use forced garbage collection as a fix. All owned file reads use FileAccess.Read and FileShare.ReadWrite | FileShare.Delete; incomplete or changing downloads must fail gracefully without blocking writers or replacement.

1. Establish a source build and standalone regression harness, with each scenario in a separate process and a timeout for nontermination.
2. Demonstrate constructor file-lock leaks for APK/IPA/APPX/APPXBundle; fix ownership and missing-entry handling. Cover valid construction and repeated disposal.
3. Demonstrate malformed Android chunk and missing APPXBundle application hangs; validate chunk traversal and bound XML iteration.
4. Reproduce and fix uppercase extensions, inverted booleans, ZIP entry zero image lookup, update parsing/defaults and related deterministic resource leaks.
5. Review additional parser/image/COM paths. Record remaining limitations without claiming a complete security proof.
6. Build release and satellite resources, run negative and normal-package regressions, request independent review and resolve important findings.
7. Fork/push, tag and publish release archive/checksums. Preserve a rollback copy, unregister old assembly, replace with the tested artifacts, register new assembly, verify identity and real shell behavior.

Validation: baseline failures must become successes without GC, hangs must terminate within subprocess timeout, full source build must succeed, and deployment uses exactly the release DLL hash. Report dependency warnings and any acceptance gaps.

Final scope additions: modern AAPT2 sparse/offset16/compact/type-offset support; updated native WebP1.6.0 and alpha-preserving decoder; all-format28%/60% badges; user explicitly authorized Explorer restart for deployment.
