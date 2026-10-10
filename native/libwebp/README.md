# libwebp 1.6.0 native runtime

Source: https://github.com/webmproject/libwebp/tree/v1.6.0
Pinned commit: 4fa21912338357f89e4fd51cf2368325b59e9bd9
License and patent grant: COPYING and PATENTS in this directory.

Built locally from the unmodified official sources for x86_64-windows-gnu and x86-windows-gnu using Zig 0.15.2 `cc -shared -O2 -DWEBP_DLL -DWEBP_USE_THREAD`. Recipe: tools/build-native.ps1. Only the resulting DLLs are release payloads. GNU Windows runtime support is linked by Zig; no separately installed compiler is needed to run the DLLs.

Compiler official package: https://ziglang.org/download/0.15.2/zig-x86_64-windows-0.15.2.zip
Compiler package SHA256: 3a0ed1e8799a2f8ce2a6e6290a9ff22e6906f8227865911fb7ddedc3cc14cb0c

The old checked-in wrapper submodule contains 1.0.3 binaries. The maintained build/package scripts deliberately do not ship those binaries. The main APK image reader uses a small BGRA decoder preserving transparency; the old wrapper's encoding APIs are not used in normal shell reads.
