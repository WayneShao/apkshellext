using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Xml;
using SkiaSharp;
using Svg.Skia;

namespace ApkShellext2;

// Renders Android VectorDrawable XML through the maintained Skia SVG adapter.
// The Shell-facing boundary remains System.Drawing.Bitmap; Skia types stay here.
public sealed class VectorDrawableRender
{
    private readonly XmlDocument document;
    public ApkQuickReader.ApkReader apkreader { get; set; }
    public Size size { get; set; }
    public Bitmap image => GetImage(document);
    public VectorDrawableRender() { }
    public VectorDrawableRender(XmlDocument xml) => document = xml ?? throw new ArgumentNullException(nameof(xml));
    public Bitmap getImage(XmlDocument xml) => GetImage(xml);

    // Compatibility entry point used by the older manifest fallback renderer.
    // Parsing is delegated to Skia; only the final path is copied to GDI+.
    public static GraphicsPath Convert2Path(string pathData)
    {
        var result = new GraphicsPath(FillMode.Alternate);
        using var skPath = SKPath.ParseSvgPathData(pathData ?? string.Empty);
        using var iterator = skPath.CreateRawIterator();
        var points = new SKPoint[4];
        SKPathVerb verb;
        while ((verb = iterator.Next(points)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    result.StartFigure(); result.SetMarkers();
                    result.AddLine(points[0].X, points[0].Y, points[0].X, points[0].Y); break;
                case SKPathVerb.Line:
                    result.AddLine(points[0].X, points[0].Y, points[1].X, points[1].Y); break;
                case SKPathVerb.Quad:
                    result.AddBezier(points[0].X, points[0].Y,
                        points[0].X + (points[1].X - points[0].X) * 2f / 3f,
                        points[0].Y + (points[1].Y - points[0].Y) * 2f / 3f,
                        points[2].X + (points[1].X - points[2].X) / 3f,
                        points[2].Y + (points[1].Y - points[2].Y) / 3f,
                        points[2].X, points[2].Y); break;
                case SKPathVerb.Cubic:
                    result.AddBezier(points[0].X, points[0].Y, points[1].X, points[1].Y,
                        points[2].X, points[2].Y, points[3].X, points[3].Y); break;
                case SKPathVerb.Close: result.CloseFigure(); break;
            }
        }
        return result;
    }

    private Bitmap GetImage(XmlDocument xml)
    {
        if (xml?.DocumentElement == null) throw new InvalidDataException("Vector drawable has no root element.");
        var width = size.Width;
        var height = size.Height;
        if (width <= 0 || height <= 0)
        {
            width = ReadViewport(xml.DocumentElement, "viewportWidth", 1);
            height = ReadViewport(xml.DocumentElement, "viewportHeight", width);
        }
        using var reader = new XmlNodeReader(xml);
        using var svg = new SKSvg();
        using var picture = svg.LoadVectorDrawable(reader) ?? throw new InvalidDataException("Unable to render Android vector drawable.");
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var bounds = picture.CullRect;
            if (bounds.Width > 0 && bounds.Height > 0) canvas.Scale(width / bounds.Width, height / bounds.Height);
            canvas.DrawPicture(picture);
            canvas.Flush();
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        return new Bitmap(stream);
    }

    private static int ReadViewport(XmlElement root, string name, int fallback)
    {
        var value = root.GetAttribute(name);
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }
}
