using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

// Uses the documented Windows thumbnail-cache API. Never edits source files.
class ThumbnailRefresh {
    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IShellItem { }
    [ComImport, Guid("091162a4-bc96-411f-aae8-c5122cd03363"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISharedBitmap { void GetSharedBitmap(out IntPtr bitmap); }
    [ComImport, Guid("f676c15d-596a-4ce2-8234-33996f445db1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IThumbnailCache {
        void GetThumbnail(IShellItem item, uint size, uint flags, out ISharedBitmap bitmap, out uint cacheFlags, out Guid id);
    }
    [DllImport("shell32.dll", CharSet=CharSet.Unicode, PreserveSig=false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IShellItem item);
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
    static extern void SHChangeNotify(uint eventId, uint flags, string item1, IntPtr item2);
    static void Extract(IThumbnailCache cache, IShellItem item, uint size, uint flags, string output) {
        ISharedBitmap shared; uint state; Guid id;
        cache.GetThumbnail(item,size,flags,out shared,out state,out id);
        try {
            if(output!=null) { IntPtr handle; shared.GetSharedBitmap(out handle); using(var bitmap=Image.FromHbitmap(handle)) bitmap.Save(output,ImageFormat.Png); }
            Console.WriteLine("size="+size+" request="+flags+" cacheFlags="+state);
        } finally { Marshal.ReleaseComObject(shared); }
    }
    [STAThread]
    static int Main(string[] args) {
        if(args.Length<1) return 2;
        var cache=(IThumbnailCache)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("50ef4544-ac9f-4a8e-b21b-8a26180db13f")));
        IShellItem item=null;
        try {
            Guid iid=typeof(IShellItem).GUID;
            SHCreateItemFromParsingName(Path.GetFullPath(args[0]),IntPtr.Zero,ref iid,out item);
            if(args.Length>1) {
                Directory.CreateDirectory(args[1]);
                try { Extract(cache,item,256,1,Path.Combine(args[1],"cached-before.png")); } catch(COMException e) { Console.WriteLine("No cached preview: "+e.ErrorCode.ToString("X")); }
            }
            foreach(uint size in new uint[]{32,96,256,768,1024})
                Extract(cache,item,size,4,args.Length>1 && size==256?Path.Combine(args[1],"fresh-after.png"):null);
            SHChangeNotify(0x2000,0x5,args[0],IntPtr.Zero);
            return 0;
        } catch(Exception e) {Console.Error.WriteLine(e); return 1;}
        finally {if(item!=null)Marshal.ReleaseComObject(item);Marshal.ReleaseComObject(cache);}
    }
}
