using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ApkShellext2 {
    // Only the stable decoding API is needed for shell thumbnails. BGRA preserves
    // straight alpha and matches Format32bppArgb's byte layout on Windows.
    internal static class WebPImage {
        private const long MaximumPixels = 16L * 1024 * 1024;

        internal static Bitmap Decode(byte[] data) {
            if (data == null || data.Length == 0) throw new InvalidDataException("Empty WebP image.");
            int width, height;
            UIntPtr inputSize = new UIntPtr((uint)data.Length);
            int valid = Environment.Is64BitProcess ?
                GetInfo64(data, inputSize, out width, out height) :
                GetInfo32(data, inputSize, out width, out height);
            if (valid == 0 || width <= 0 || height <= 0 || width > 32768 || height > 32768 ||
                (long)width * height > MaximumPixels)
                throw new InvalidDataException("Invalid or oversized WebP image.");

            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            try {
                BitmapData pixels = bitmap.LockBits(new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try {
                    if (pixels.Stride < checked(width * 4))
                        throw new InvalidDataException("Unsupported WebP bitmap stride.");
                    uint size = checked((uint)((long)pixels.Stride * height));
                    IntPtr result = Environment.Is64BitProcess ?
                        Decode64(data, inputSize, pixels.Scan0, new UIntPtr(size), pixels.Stride) :
                        Decode32(data, inputSize, pixels.Scan0, new UIntPtr(size), pixels.Stride);
                    if (result == IntPtr.Zero) throw new InvalidDataException("Invalid or truncated WebP image.");
                } finally { bitmap.UnlockBits(pixels); }
                return bitmap;
            } catch { bitmap.Dispose(); throw; }
        }

        [DllImport("libwebp_x64.dll", EntryPoint = "WebPGetInfo", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetInfo64([In] byte[] data, UIntPtr size, out int width, out int height);
        [DllImport("libwebp_x86.dll", EntryPoint = "WebPGetInfo", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetInfo32([In] byte[] data, UIntPtr size, out int width, out int height);
        [DllImport("libwebp_x64.dll", EntryPoint = "WebPDecodeBGRAInto", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Decode64([In] byte[] data, UIntPtr size, IntPtr output, UIntPtr outputSize, int stride);
        [DllImport("libwebp_x86.dll", EntryPoint = "WebPDecodeBGRAInto", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Decode32([In] byte[] data, UIntPtr size, IntPtr output, UIntPtr outputSize, int stride);
    }
}
