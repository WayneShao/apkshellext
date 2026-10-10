# v0.4 component refresh

This branch replaces the embedded SharpZipLib and SVG.NET source trees.

- `System.IO.Compression.ZipArchive` is the ZIP/APK/IPA/APPX reader implementation.
- `Svg.Skia` 5.2.3 with SkiaSharp 4.148.0 renders Android VectorDrawable XML and standard SVG.
- `Compatibility/ZipArchiveCompat.cs` preserves the existing reader-facing entry API while enforcing the existing size limits and shared read semantics.
- `VectorDrawableRender` keeps `System.Drawing.Bitmap` at the Shell boundary and contains all Skia types inside the rendering adapter.

The release package must include the SkiaSharp Windows native assets and the corresponding notices. Full APK, IPA, APPX, vector drawable and Shell callback regression tests are required before this branch can replace v0.3.
