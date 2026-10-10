// Ported from PNGDecrush (MIT), retaining the original public API.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.IO.Hashing;

namespace PNGDecrush
{
    public class PNGDecrusher
    {
        public static void Decrush(Stream input, Stream output)
        {
            using (var fixedChunks = new MemoryStream())
            {
                DecrushAtChunkLevel(input, fixedChunks);
                fixedChunks.Position = 0;
                DecrushAtPixelLevel(fixedChunks, output);
            }
        }

        private static void DecrushAtChunkLevel(Stream input, Stream output)
        {
            PNGChunkParser.WriteChunksAsPNG(DecrushChunks(PNGChunkParser.ChunksFromStream(input)), output);
        }

        public static IEnumerable<PNGChunk> DecrushChunks(IEnumerable<PNGChunk> chunks)
        {
            if (chunks == null) throw new ArgumentNullException("chunks");
            var source = chunks.ToList();
            var withoutCgBI = source.Where(c => c.Type != PNGChunk.ChunkType.CgBI).ToList();
            if (withoutCgBI.Count == source.Count)
                throw new InvalidDataException("Could not find a CgBI chunk. Image wasn't crushed with Apple's -iohone option.");
            return ConvertIDATChunksFromDeflateToZlib(withoutCgBI);
        }

        private static IEnumerable<PNGChunk> ConvertIDATChunksFromDeflateToZlib(IList<PNGChunk> chunks)
        {
            var idat = chunks.Where(c => c.Type == PNGChunk.ChunkType.IDAT).ToList();
            if (idat.Count == 0) throw new InvalidDataException("CgBI image has no IDAT chunks.");
            byte[] raw = idat.SelectMany(c => c.Data).ToArray();
            byte[] zlib = ConvertDeflateToZlib(raw);
            var replacement = SplitBufferIntoChunks(zlib, idat.Count).Select(IDATChunkWithBytes).ToList();
            int first = chunks.IndexOf(idat[0]);
            var result = chunks.Where(c => c.Type != PNGChunk.ChunkType.IDAT).ToList();
            result.InsertRange(first, replacement);
            return result;
        }

        public static IEnumerable<byte[]> SplitBufferIntoChunks(byte[] input, int numberOfChunks)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (numberOfChunks < 1) throw new ArgumentOutOfRangeException("numberOfChunks");
            var result = new List<byte[]>(); int left = input.Length, index = 0;
            for (int count = numberOfChunks; count > 0; --count)
            {
                int size = Math.Min((int)Math.Ceiling((double)left / count), left);
                var part = new byte[size]; Array.Copy(input, index, part, 0, size);
                result.Add(part); index += size; left -= size;
            }
            return result;
        }

        private static PNGChunk IDATChunkWithBytes(byte[] data)
        {
            const string type = "IDAT";
            return new PNGChunk(type, data, CalculateCRCForChunk(type, data));
        }

        private static byte[] ConvertDeflateToZlib(byte[] input)
        {
            using (var rawInput = new MemoryStream(input, false))
            using (var inflater = new DeflateStream(rawInput, CompressionMode.Decompress))
            using (var zlibData = new MemoryStream())
            {
                using (var compressor = new ZLibStream(zlibData, CompressionMode.Compress, true))
                    inflater.CopyTo(compressor);
                return zlibData.ToArray();
            }
        }

        public static uint CalculateCRCForChunk(string chunkType, byte[] chunkData)
        {
            if (chunkType == null || chunkType.Length != 4) throw new ArgumentException("PNG chunk type must have four characters.", "chunkType");
            if (chunkData == null) throw new ArgumentNullException("chunkData");
            byte[] input = new byte[4 + chunkData.Length];
            Encoding.ASCII.GetBytes(chunkType, 0, 4, input, 0);
            Buffer.BlockCopy(chunkData, 0, input, 4, chunkData.Length);
            return Crc32.HashToUInt32(input);
        }

        public static void DecrushAtPixelLevel(Stream input, Stream output)
        {
            using (var bitmap = new Bitmap(input))
            {
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
                try { FixPixelsInBitmapData(data); }
                finally { bitmap.UnlockBits(data); }
                bitmap.Save(output, ImageFormat.Png);
            }
        }

        private static void FixPixelsInBitmapData(BitmapData data)
        {
            bool alpha; uint bpp;
            if (data.PixelFormat == PixelFormat.Format32bppArgb) { alpha = true; bpp = 4; }
            else if (data.PixelFormat == PixelFormat.Format32bppRgb) { alpha = false; bpp = 4; }
            else throw new InvalidDataException("Only 32 bit RGB(A) PNGs are supported by PNGDecrusher");
            int length = checked(Math.Abs(data.Stride) * data.Height); var bytes = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            for (uint i = 0; i + bpp <= bytes.Length; i += bpp)
            {
                byte t = bytes[i]; bytes[i] = bytes[i + 2]; bytes[i + 2] = t;
                if (alpha && bytes[i + 3] != 0)
                {
                    byte a = bytes[i + 3];
                    bytes[i] = (byte)Math.Min(255, bytes[i] * 255 / a);
                    bytes[i + 1] = (byte)Math.Min(255, bytes[i + 1] * 255 / a);
                    bytes[i + 2] = (byte)Math.Min(255, bytes[i + 2] * 255 / a);
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
    }
}
