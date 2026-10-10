using System;
using System.Drawing;
using System.Drawing.Imaging;
using ApkShellext2;

class OverlayRegression
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Bitmap Solid(int size, Color color)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(color);
        return bitmap;
    }
    static void Test(int size)
    {
        int edge = Math.Max(1, (int)Math.Round(size * 0.28));
        int margin = (int)Math.Round(size * 0.04);
        var expected = new Rectangle(margin, size - margin - edge, edge, edge);
        using (var main = Solid(size, Color.Transparent))
        using (var marker = Solid(16, Color.Red))
        using (var output = TypeOverlay.Compose(main, marker, new Size(size, size)))
        {
            Require(output.Size == main.Size, "Output size changed");
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                Color pixel = output.GetPixel(x, y);
                if (!expected.Contains(x, y)) Require(pixel.A == 0, "Marker exceeds approved bounds at " + x + "," + y);
                else Require(Math.Abs(pixel.A - 153) <= 1, "Marker does not fill approved bounds at " + x + "," + y);
            }
            Color center = output.GetPixel(expected.Left + edge / 2, expected.Top + edge / 2);
            Require(Math.Abs(center.A - 153) <= 1, "Marker opacity is " + center.A + ", expected 153");
            Require(main.GetPixel(0, 0).A == 0 && marker.GetPixel(0, 0).R == 255, "Borrowed input mutated or disposed");
        }
        using (var main = Solid(size, Color.Blue))
        using (var marker = Solid(16, Color.Red))
        using (var output = TypeOverlay.Compose(main, marker, new Size(size, size)))
        {
            if (size >= 16) Require(output.GetPixel(size / 2, size / 2).ToArgb() == Color.Blue.ToArgb(), "Main icon shifted or faded");
            Color center = output.GetPixel(expected.Left + edge / 2, expected.Top + edge / 2);
            Require(center.A == 255 && Math.Abs(center.R - 153) <= 1 && Math.Abs(center.B - 102) <= 1, "Incorrect alpha blend");
        }
        Console.WriteLine("PASS overlay " + size + "px");
    }
    static void Preview(string path)
    {
        var types = new[] { AppPackageReader.AppType.AndroidApp, AppPackageReader.AppType.iOSApp, AppPackageReader.AppType.WindowsPhoneApp, AppPackageReader.AppType.WindowsPhoneAppBundle };
        string[] names = { "APK / Android", "IPA / Apple", "APPX / Windows", "APPXBUNDLE" };
        int[] sizes = { 32, 64, 128 };
        using (var sheet = new Bitmap(640, 630))
        using (var graphics = Graphics.FromImage(sheet))
        using (var font = new Font("Segoe UI", 11))
        {
            graphics.Clear(Color.FromArgb(242, 244, 247));
            graphics.DrawString("Type badge: 28% size / 60% opacity / 4% margin", font, Brushes.Black, 12, 10);
            for (int row = 0; row < types.Length; row++) {
                int top = 45 + row * 145;
                graphics.DrawString(names[row], font, Brushes.Black, 12, top + 45);
                for (int column = 0; column < sizes.Length; column++) {
                    int size = sizes[column]; int left = 195 + column * 145;
                    using (var main = Solid(size, Color.FromArgb(34, 112, 195)))
                    {
                        using (var g = Graphics.FromImage(main)) {
                            g.FillEllipse(Brushes.White, size * 0.25f, size * 0.18f, size * 0.5f, size * 0.5f);
                            g.FillRectangle(Brushes.Orange, size * 0.39f, size * 0.32f, size * 0.22f, size * 0.35f);
                        }
                        // ResourceManager owns this shared marker; the helper only borrows it.
                        using (var output = TypeOverlay.Compose(main, Utility.AppTypeIcon(types[row]), new Size(size, size)))
                            graphics.DrawImageUnscaled(output, left, top);
                    }
                    graphics.DrawString(size + "px", font, Brushes.Black, left, top + 128);
                }
            }
            sheet.Save(path, ImageFormat.Png);
        }
        Console.WriteLine("Preview: " + path);
    }
    static int Main(string[] args)
    {
        try {
            foreach (int size in new[] { 32, 64, 128, 16, 2, 1 }) Test(size);
            if (args.Length != 0) Preview(args[0]);
            return 0;
        } catch (Exception e) { Console.WriteLine("FAIL " + e); return 1; }
    }
}

#if LEGACY
namespace ApkShellext2
{
    internal static class TypeOverlay
    {
        internal static Bitmap Compose(Bitmap main, Bitmap marker, Size size)
        {
            return Utility.CombineBitmap(main, marker, new Rectangle(Point.Empty, size),
                new Rectangle(0, size.Height / 2, size.Width / 2, size.Height / 2), size);
        }
    }
}
#endif
