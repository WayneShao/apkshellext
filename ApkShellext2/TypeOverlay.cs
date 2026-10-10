using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ApkShellext2
{
    /// <summary>Composes a subtle type badge while borrowing both input bitmaps.</summary>
    internal static class TypeOverlay
    {
        internal static Bitmap Compose(Bitmap main, Bitmap marker, Size size)
        {
            if (main == null) throw new ArgumentNullException("main");
            if (marker == null) throw new ArgumentNullException("marker");
            if (size.Width < 1 || size.Height < 1) throw new ArgumentOutOfRangeException("size");
            int unit = Math.Min(size.Width, size.Height);
            int edge = Math.Max(1, (int)Math.Round(unit * 0.28));
            int margin = (int)Math.Round(unit * 0.04);
            var destination = new Rectangle(margin, size.Height - margin - edge, edge, edge);
            var result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(result))
                using (var attributes = new ImageAttributes())
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.DrawImage(main, new Rectangle(Point.Empty, size), 0, 0, main.Width, main.Height, GraphicsUnit.Pixel, attributes);
                    graphics.CompositingMode = CompositingMode.SourceOver;
                    var opacity = new ColorMatrix();
                    opacity.Matrix33 = 0.60f;
                    attributes.SetColorMatrix(opacity);
                    graphics.DrawImage(marker, destination, 0, 0, marker.Width, marker.Height, GraphicsUnit.Pixel, attributes);
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }
    }
}
