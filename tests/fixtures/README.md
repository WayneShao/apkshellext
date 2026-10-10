# Generated WebP fixture

`alpha-lossless.webp` is a generated 3 x 2 lossless RGBA image, not a downloaded application asset.

Pixels, in row order:

```text
(255, 0, 0, 255), (0, 255, 0, 128), (0, 0, 255, 0)
(20, 40, 60, 255), (80, 100, 120, 64), (140, 160, 180, 192)
```

Generated with Pillow 12.0.0:

```python
from PIL import Image
image = Image.new("RGBA", (3, 2))
image.putdata([(255,0,0,255), (0,255,0,128), (0,0,255,0),
               (20,40,60,255), (80,100,120,64), (140,160,180,192)])
image.save("alpha-lossless.webp", lossless=True, exact=True)
```

`tools/test-webp.ps1 -Baseline` deliberately runs the legacy managed decoder and fails the half-alpha assertion (255 instead of 128). The normal test runs the dedicated BGRA decoder in both x64 and x86 processes, checks native version 1.6.0, and exercises successful and failed decoding.
