using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

class ShellSmoke {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct Info { public IntPtr icon; public int index; public uint attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string display; [MarshalAs(UnmanagedType.ByValTStr, SizeConst=80)] public string type; }
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attributes, ref Info info, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    public static int Main(string[] args) {
        var dir = Path.Combine(Path.GetTempPath(), "ApkShellSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try {
            for (int i=0;i<5;i++) {
                string file = Path.Combine(dir, "download-"+i+".apk");
                File.WriteAllBytes(file, new byte[0]);
                var info = new Info();
                if (SHGetFileInfo(file, 0, ref info, (uint)Marshal.SizeOf(info), 0x100) == IntPtr.Zero) throw new Exception("Shell icon extraction failed");
                if (info.icon != IntPtr.Zero) DestroyIcon(info.icon);
                using (var writer = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) writer.WriteByte(0x50);
                File.Move(file, file+".renamed");
                File.Delete(file+".renamed");
            }
            bool loaded = false;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) if (assembly.GetName().Name == "ApkShellext2") {
                Console.WriteLine("Loaded "+assembly.FullName+" from "+assembly.Location);
                if(args.Length>0 && assembly.GetName().Version.ToString()!=args[0]) throw new Exception("Unexpected installed version");
                loaded = true;
            }
            if (!loaded) throw new Exception("Shell did not load the registered managed icon handler in this process");
            Console.WriteLine("PASS registered Shell icon callbacks, staged writes/rename/delete, no GC");
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { try { Directory.Delete(dir,true); } catch {} }
    }
}
