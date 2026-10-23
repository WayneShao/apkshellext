using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

class IconCacheVisual {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct Info { public IntPtr icon; public int index; public uint attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string display;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=80)] public string type; }
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path,uint attr,ref Info info,uint size,uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [STAThread] static int Main(string[] args) {
        if(args.Length<4) { Console.Error.WriteLine("Usage: output-folder distinct-icon-1.apk distinct-icon-2.apk distinct-icon-3.apk ...");return 2; }
        Directory.CreateDirectory(args[0]); var hashes=new HashSet<string>();
        using var sheet=new Bitmap(580,(args.Length-1)*84);
        using var g=Graphics.FromImage(sheet);g.Clear(Color.White);
        for(int i=1;i<args.Length;i++) {
            var info=new Info();
            if(SHGetFileInfo(args[i],0,ref info,(uint)Marshal.SizeOf<Info>(),0x100|0x4000)==IntPtr.Zero || info.icon==IntPtr.Zero) throw new Exception("Shell extraction failed: "+args[i]);
            try {
                using var icon=Icon.FromHandle(info.icon);using var bitmap=icon.ToBitmap();
                using var bytes=new MemoryStream();bitmap.Save(bytes,ImageFormat.Png);
                string hash=Convert.ToHexString(SHA256.HashData(bytes.ToArray()));hashes.Add(hash);
                bitmap.Save(Path.Combine(args[0],i+".png"),ImageFormat.Png);
                g.DrawImage(bitmap,8,(i-1)*84,64,64);
                g.DrawString(Path.GetFileName(args[i]),SystemFonts.DefaultFont,Brushes.Black,84,(i-1)*84+20);
                Console.WriteLine($"{Path.GetFileName(args[i])} index={info.index} hash={hash}");
            } finally { DestroyIcon(info.icon); }
        }
        sheet.Save(Path.Combine(args[0],"shell-icons.png"),ImageFormat.Png);
        if(hashes.Count!=args.Length-1) { Console.Error.WriteLine("FAIL different fixture icons were aliased in Shell cache");return 1; }
        Console.WriteLine("PASS distinct actual Shell images: "+hashes.Count);return 0;
    }
}
