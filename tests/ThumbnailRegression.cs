using System;
using System.Drawing;
using System.Runtime.InteropServices;
using SharpShell.Interop;
using SharpShell.SharpThumbnailHandler;

class ThumbnailRegression
{
    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    static extern int GetObject(IntPtr handle, int size, out NativeBitmap bitmap);
    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)]
    struct NativeBitmap { public int Type, Width, Height, Stride; public ushort Planes, Bits; public IntPtr Pixels; }
    class Handler : SharpThumbnailHandler
    {
        internal Bitmap Returned;
        internal bool Fail;
        protected override Bitmap GetThumbnailImage(uint width)
        {
            if (Fail) throw new InvalidOperationException("Fixture failure");
            return Returned;
        }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static int Main()
    {
        IntPtr handle = IntPtr.Zero;
        Bitmap source = new Bitmap(16, 16);
        try {
            var handler = new Handler { Returned = source };
            WTS_ALPHATYPE alpha;
            int hr = ((IThumbnailProvider)handler).GetThumbnail(16, out handle, out alpha);
            Require(hr == 0 && handle != IntPtr.Zero && alpha == WTS_ALPHATYPE.WTSAT_ARGB, "Successful transfer failed");
            NativeBitmap native;
            Require(GetObject(handle, Marshal.SizeOf(typeof(NativeBitmap)), out native) != 0 && native.Width == 16 && native.Height == 16, "Transferred HBITMAP invalid");
            bool disposed = false;
            try { int ignored = source.Width; } catch (ArgumentException) { disposed = true; }
            Require(disposed, "SharpThumbnailHandler retained its returned Bitmap after HBITMAP transfer");
            Console.WriteLine("PASS thumbnail Bitmap disposed, shell-owned HBITMAP valid");
            Require(DeleteObject(handle), "Test caller could not free its HBITMAP"); handle = IntPtr.Zero;
            handler.Returned = null;
            Require(((IThumbnailProvider)handler).GetThumbnail(16, out handle, out alpha) != 0 && handle == IntPtr.Zero, "Null thumbnail did not fail cleanly");
            handler.Fail = true;
            Require(((IThumbnailProvider)handler).GetThumbnail(16, out handle, out alpha) != 0 && handle == IntPtr.Zero, "Thumbnail exception did not fail cleanly");
            Console.WriteLine("PASS thumbnail null and exception return no HBITMAP");
            return 0;
        } catch (Exception error) { Console.WriteLine("FAIL " + error); return 1; }
        finally { source.Dispose(); if (handle != IntPtr.Zero) DeleteObject(handle); }
    }
}
