# ApkShellext2 v0.5.1 — per-file Shell icon cache identity

v0.5 returned the same empty icon location and index zero for every package. Windows could reuse one app's icon for unrelated APKs, even though the isolated worker extracted the correct distinct images. This release gives each package a compact ASCII cache identity derived from its full Unicode path, file identity, length and modification metadata. ANSI and Unicode COM callers receive the same identity. Incomplete/unavailable metadata and insufficient output buffers disable caching rather than truncate an identity into a collision.

The native/.NET10 worker isolation introduced in v0.5 is retained. File metadata handles allow shared read/write/delete and close within GetIconLocation. Default-icon requests now return S_FALSE, allowing Shell to choose its generic loading placeholder rather than reusing an arbitrary application's icon.

Regression: v0.5 returned identical Shell image index50 and identical PNG hashes for BandLab, Spotify and AIDA64; the worker returned their correct distinct icons. Native identity tests cover different paths, same basenames in different folders, file rewrite/growth, Unicode and ANSI callers, short buffers and incomplete files. tests/IconCacheVisual.csproj verifies actual registered SHGetFileInfo images, not merely successful HICON creation.

Install the complete package with install.bat as administrator, restart Explorer and refresh the affected folder. Existing folders may retain old icon cache entries until refreshed. Requires x64 .NET10 Desktop Runtime for the isolated worker. Original-file permissions and default APK opener are preserved.
