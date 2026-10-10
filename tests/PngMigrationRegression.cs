using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Imaging;
using PNGDecrush;

class PngMigrationRegression
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static byte[] Inflate(byte[] zlib, bool raw)
    {
        using (var input = new MemoryStream(zlib, raw ? 0 : 2, raw ? zlib.Length : zlib.Length - 6))
        using (var stream = new DeflateStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream()) { stream.CopyTo(output); return output.ToArray(); }
    }
    static void TestCrc()
    {
        Require(PNGDecrusher.CalculateCRCForChunk("IEND", new byte[0]) == 0xae426082U, "IEND CRC mismatch");
    }
    static void TestRawDeflate()
    {
        byte[] raw;
        using (var output = new MemoryStream())
        using (var stream = new DeflateStream(output, CompressionMode.Compress, true)) { stream.Write(new byte[] { 1, 2, 3, 4 }, 0, 4); stream.Close(); raw = output.ToArray(); }
        var chunks = new[] { new PNGChunk("CgBI", new byte[0], 0), new PNGChunk("IDAT", raw, 0), new PNGChunk("IDAT", new byte[0], 0) };
        var result = PNGDecrusher.DecrushChunks(chunks).Where(x => x.Type == PNGChunk.ChunkType.IDAT).ToArray();
        Require(Inflate(result.SelectMany(x => x.Data).ToArray(), false).SequenceEqual(new byte[] { 1, 2, 3, 4 }), "Raw DEFLATE migration roundtrip failed");
    }
    static void TestNoIonic()
    {
        Require(typeof(PNGDecrusher).Assembly.GetTypes().All(x => x.Namespace == null || !x.Namespace.StartsWith("Ionic.")), "Ionic codec types remain in production assembly");
    }
    static byte[] Chunk(string type, byte[] data)
    {
        using (var output = new MemoryStream()) using (var writer = new BinaryWriter(output))
        {
            writer.Write(new byte[]{(byte)(data.Length>>24),(byte)(data.Length>>16),(byte)(data.Length>>8),(byte)data.Length});
            writer.Write(System.Text.Encoding.ASCII.GetBytes(type)); writer.Write(data);
            uint crc=PNGDecrusher.CalculateCRCForChunk(type,data); writer.Write(new byte[]{(byte)(crc>>24),(byte)(crc>>16),(byte)(crc>>8),(byte)crc});
            return output.ToArray();
        }
    }
    static byte[] CgbiPng()
    {
        // 2x1 BGRA scanline: transparent blue, opaque red. Split raw IDAT to exercise concatenation.
        byte[] scanline = { 0, 255, 0, 0, 0, 0, 0, 255, 255 };
        byte[] raw;
        using (var output = new MemoryStream()) { using (var stream = new DeflateStream(output, CompressionMode.Compress, true)) stream.Write(scanline, 0, scanline.Length); raw = output.ToArray(); }
        using (var png = new MemoryStream())
        using (var writer = new BinaryWriter(png))
        {
            writer.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            writer.Write(Chunk("IHDR", new byte[] { 0, 0, 0, 2, 0, 0, 0, 1, 8, 6, 0, 0, 0 }));
            writer.Write(Chunk("CgBI", new byte[0]));
            int split = raw.Length / 2; writer.Write(Chunk("IDAT", raw.Take(split).ToArray())); writer.Write(Chunk("IDAT", raw.Skip(split).ToArray()));
            writer.Write(Chunk("IEND", new byte[0])); return png.ToArray();
        }
    }
    static void TestCgbiPixels()
    {
        using (var input = new MemoryStream(CgbiPng())) using (var output = new MemoryStream())
        {
            PNGDecrusher.Decrush(input, output); output.Position = 0;
            using (var bitmap = new Bitmap(output))
            {
                Color transparent = bitmap.GetPixel(0, 0), opaque = bitmap.GetPixel(1, 0);
                Require(transparent.A == 0, "Transparent CgBI alpha was not preserved");
                Require(opaque.R > 240 && opaque.G < 15 && opaque.B < 15 && opaque.A == 255, "Opaque CgBI pixel channel order was not restored");
            }
        }
    }
    static int Main()
    {
        try { TestCrc(); TestRawDeflate(); TestCgbiPixels(); TestNoIonic(); Console.WriteLine("PASS PNG migration CRC/raw-deflate/CgBI/no-Ionic"); return 0; }
        catch (Exception e) { Console.WriteLine("FAIL " + e); return 1; }
    }
}
