# Third-party notices


- SharpShell (including local .NET10 compatibility copies): Dave Kerr, MIT; licenses/SharpShell/LICENSE.md.
- PNGDecrush (including the BCL compression port): Michael Weller, MIT; licenses/PNGDecrush/LICENSE.
- PlistCS: Animetrics Inc., MIT notice reproduced from the source header in PlistCS.txt.
- QRCoder and WebP-wrapper: upstream notices included in their named license folders.
- Native libwebp1.6.0: COPYING and PATENTS included with source commit/build provenance.
- Microsoft System.ComponentModel.Composition and System.IO.Hashing10.0.12: .NET runtime MIT license and third-party notices included. Windows Desktop APIs are supplied by the separately installed Microsoft .NET10 runtime.

Compatibility copies are deliberately kept outside submodules so upstream pins remain reproducible. Their original notices continue to apply.

- System.IO.Compression: .NET 10 runtime ZIP implementation for APK, IPA and APPX reading.
- Svg.Skia, Svg.Model, Svg.Animation, Svg.SceneGraph and ShimSkiaSharp 5.2.3: MIT; svg-skia.txt from upstream commit 7910666415a96a09643d1729eb5da5d115d75748.
- Svg.Custom 5.2.3: MS-PL; svg-custom.txt from the same upstream commit. Svg.Skia uses this maintained parser dependency; removing the historical embedded SVG source did not remove every Svg namespace from the dependency graph.
- ExCSS 4.3.1: MIT; excss.txt from upstream commit c97e84d6126bb2e42658cf5af627d52e697c8779.
- SkiaSharp 4.148.0 and HarfBuzzSharp 14.2.0: MIT and native third-party notices, copied verbatim from their pinned NuGet packages (skiasharp.txt, harfbuzzsharp.txt, skiasharp-native-notices.txt, harfbuzz-native-notices.txt).
- The C++ Shell bridge is part of this MIT-licensed project and is built using Zig's Windows toolchain. Its Windows imports are supplied by the operating system.
