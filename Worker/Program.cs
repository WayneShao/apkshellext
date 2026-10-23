using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using ApkShellext2;
using SharpShell.SharpInfoTipHandler;

[assembly: ComVisible(false)]

namespace ApkShellext2.Worker
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Dependency diagnostic writes must never become protocol bytes.
            Console.SetOut(TextWriter.Null);
            try
            {
                if (args.Length == 2 && args[0] == "--associations")
                    return Associations(args[1]);
                if (args.Length >= 2 && args[0] == "--menu")
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                    using (var form = new MenuWindow(args.Skip(1).Select(Path.GetFullPath).ToArray()))
                        Application.Run(form);
                    return 0;
                }
                if (args.Length == 2 && args[0] == "--info")
                {
                    var handler = new InfoHandler();
                    ((IPersistFile)handler).Load(Path.GetFullPath(args[1]), 0);
                    string value = handler.Read() ?? "";
                    int count = Math.Min(value.Length, 32768);
                    if (count < value.Length && count > 0 && char.IsHighSurrogate(value[count - 1])) count--;
                    byte[] bytes = Encoding.Unicode.GetBytes(value.Substring(0, count));
                    using (var writer = new BinaryWriter(Console.OpenStandardOutput()))
                    {
                        writer.Write((uint)count);
                        writer.Write(bytes);
                    }
                    return 0;
                }
                if (args.Length != 3 || (args[0] != "--icon" && args[0] != "--thumbnail") ||
                    !int.TryParse(args[1], out int size) || size < 1 || size > 1024)
                    return 2;
                string path = Path.GetFullPath(args[2]);
                using (Bitmap bitmap = args[0] == "--icon" ? ReadIcon(path, size) : ReadThumbnail(path, size))
                {
                    if (bitmap == null) return 3;
                    WriteBitmap(bitmap, size);
                }
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error.GetBaseException().Message);
                return 1;
            }
        }

        private static int Associations(string operation)
        {
            if (operation != "install" && operation != "uninstall") return 2;
            Type registration = typeof(ApkIconHandler).Assembly.GetType("ApkShellext2.ShellRegistration", true);
            MethodInfo method = registration.GetMethod(operation == "install" ? "Register" : "Unregister",
                BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Type) }, null);
            foreach (Type handler in new[] { typeof(ApkIconHandler), typeof(ApkThumbnailHandler), typeof(ApkInfoTipHandler), typeof(ApkContextMenu) })
                method.Invoke(null, new object[] { handler });
            return 0;
        }

        private static Bitmap ReadIcon(string path, int size)
        {
            using (var handler = new IconHandler())
            {
                ((IPersistFile)handler).Load(path, 0);
                using (Icon icon = handler.Read((uint)size))
                {
                    if (icon == null) return null;
                    // GetIcon transfers a newly allocated native HICON to its caller.
                    IntPtr handle = icon.Handle;
                    try { return icon.ToBitmap(); }
                    finally { DestroyIcon(handle); }
                }
            }
        }

        private static Bitmap ReadThumbnail(string path, int size)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var handler = new ThumbnailHandler();
                handler.Initialize(new FileComStream(stream), 0);
                try { return handler.Read((uint)size); }
                finally { handler.SelectedItemStream?.Dispose(); }
            }
        }

        internal static void WriteBitmap(Bitmap bitmap, int maximumSize)
        {
            if (bitmap.Width < 1 || bitmap.Height < 1 || bitmap.Width > maximumSize || bitmap.Height > maximumSize)
                throw new InvalidDataException("Invalid image dimensions.");
            // Draw onto PArgb explicitly: GDI+ ARGB bytes are not premultiplied.
            using (var output = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppPArgb))
            {
                using (Graphics graphics = Graphics.FromImage(output))
                {
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    graphics.DrawImageUnscaled(bitmap, 0, 0);
                }
                var rectangle = new Rectangle(0, 0, output.Width, output.Height);
                BitmapData data = output.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    // Stage the full payload before writing its header to avoid partial success.
                    byte[] pixels = new byte[checked(output.Width * output.Height * 4)];
                    int rowBytes = output.Width * 4;
                    for (int row = 0; row < output.Height; row++)
                        Marshal.Copy(IntPtr.Add(data.Scan0, row * data.Stride), pixels, row * rowBytes, rowBytes);
                    using (var writer = new BinaryWriter(Console.OpenStandardOutput()))
                    {
                        writer.Write((uint)output.Width);
                        writer.Write((uint)output.Height);
                        writer.Write(pixels);
                    }
                }
                finally { output.UnlockBits(data); }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        private sealed class IconHandler : ApkIconHandler, IDisposable
        {
            internal Icon Read(uint size) => GetIcon(size <= 16, size);
            public void Dispose()
            {
                // The legacy handler caches its decoded bitmap for an Explorer lifetime.
                // The worker owns that cache and explicitly releases it after this request.
                FieldInfo cache = typeof(ApkIconHandler).GetField("m_icon", BindingFlags.Instance | BindingFlags.NonPublic);
                (cache.GetValue(this) as Bitmap)?.Dispose();
                cache.SetValue(this, null);
            }
        }
        private sealed class InfoHandler : ApkInfoTipHandler
        {
            internal string Read() => GetInfo(RequestedInfoType.InfoTip, false);
        }
        private sealed class ThumbnailHandler : ApkThumbnailHandler
        {
            internal Bitmap Read(uint size) => GetThumbnailImage(size);
        }
        private sealed class MenuHandler : ApkContextMenu
        {
            internal ContextMenuStrip Read(string[] paths)
            {
                ((ICollection<string>)SelectedItemPaths).Clear();
                foreach (string path in paths) ((ICollection<string>)SelectedItemPaths).Add(path);
                return CreateMenu();
            }
        }

        private sealed class MenuWindow : Form
        {
            private readonly ContextMenuStrip menu;
            private readonly MenuHandler handler = new MenuHandler();
            internal MenuWindow(string[] paths)
            {
                Text = "ApkShellext2";
                StartPosition = FormStartPosition.CenterScreen;
                Size = new Size(620, 220);
                MinimumSize = new Size(420, 180);
                var description = new Label { Dock = DockStyle.Fill, Padding = new Padding(16), AutoEllipsis = true,
                    Text = string.Join(Environment.NewLine, paths.Take(4)) + (paths.Length > 4 ? Environment.NewLine + "…" : "") };
                Controls.Add(description);
                var strip = new MenuStrip();
                Controls.Add(strip);
                MainMenuStrip = strip;
                // Reuse the actual menu tree and event handlers, including preferences.
                menu = handler.Read(paths);
                while (menu.Items.Count > 0) strip.Items.Add(menu.Items[0]);
                Shown += (_, __) => { if (strip.Items.Count > 0) ((ToolStripMenuItem)strip.Items[0]).ShowDropDown(); };
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) menu?.Dispose();
                base.Dispose(disposing);
            }
        }

        // Managed read-only IStream lets the original thumbnail implementation consume
        // the native shim's snapshot directly, without another temporary extraction.
        private sealed class FileComStream : IStream
        {
            private readonly Stream stream;
            internal FileComStream(Stream stream) { this.stream = stream; }
            public void Read(byte[] pv, int cb, IntPtr read) { int count = stream.Read(pv, 0, cb); if (read != IntPtr.Zero) Marshal.WriteInt32(read, count); }
            public void Seek(long offset, int origin, IntPtr position) { long result = stream.Seek(offset, (SeekOrigin)origin); if (position != IntPtr.Zero) Marshal.WriteInt64(position, result); }
            public void Stat(out STATSTG stat, int flag) { stat = new STATSTG { cbSize = stream.Length, type = 2, grfMode = 0 }; }
            public void Write(byte[] pv, int cb, IntPtr written) => throw new NotSupportedException();
            public void SetSize(long size) => throw new NotSupportedException();
            public void CopyTo(IStream target, long cb, IntPtr read, IntPtr written) => throw new NotSupportedException();
            public void Commit(int flags) => throw new NotSupportedException();
            public void Revert() => throw new NotSupportedException();
            public void LockRegion(long offset, long cb, int type) => throw new NotSupportedException();
            public void UnlockRegion(long offset, long cb, int type) => throw new NotSupportedException();
            public void Clone(out IStream result) => throw new NotSupportedException();
        }
    }
}
