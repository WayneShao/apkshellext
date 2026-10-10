using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using SharpShell.Helpers;
using STATSTG = System.Runtime.InteropServices.ComTypes.STATSTG;

class ComStreamRegression
{
    [DllImport("shlwapi.dll", PreserveSig = true)]
    static extern IntPtr SHCreateMemStream(byte[] data, uint size);

    static void Equal(long expected, long actual)
    {
        if (expected != actual) throw new Exception("Expected " + expected + ", got " + actual);
    }
    static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    static void Run(string name)
    {
        var fake = new FakeStream(new byte[] { 10, 20, 30, 40 });
        var stream = new ComStream(fake);
        var buffer = new byte[] { 99, 99, 99, 99, 99, 99 };
        switch (name)
        {
            case "offset":
                Equal(3, stream.Read(buffer, 2, 3));
                Equal(99, buffer[1]); Equal(10, buffer[2]); Equal(30, buffer[4]); Equal(99, buffer[5]); break;
            case "short_read":
                fake.MaximumRead = 2; Equal(2, stream.Read(buffer, 0, 6)); Equal(2, stream.Position); break;
            case "eof":
                stream.Seek(0, SeekOrigin.End); Equal(0, stream.Read(buffer, 0, 6)); Equal(4, stream.Position); break;
            case "initial_position":
                fake.Seek(2, 0, IntPtr.Zero); Equal(2, stream.Position); break;
            case "external_position":
                stream.Read(buffer, 0, 1); fake.Seek(3, 0, IntPtr.Zero); Equal(3, stream.Position); break;
            case "seek_end":
                Equal(3, stream.Seek(-1, SeekOrigin.End)); Equal(3, stream.Position); break;
            case "seek_current":
                fake.Seek(2, 0, IntPtr.Zero); Equal(3, stream.Seek(1, SeekOrigin.Current)); Equal(3, stream.Position); break;
            case "arguments":
                Throws<ArgumentNullException>(() => stream.Read(null, 0, 1));
                Throws<ArgumentOutOfRangeException>(() => stream.Read(buffer, -1, 1));
                Throws<ArgumentOutOfRangeException>(() => stream.Read(buffer, 0, -1));
                Throws<ArgumentException>(() => stream.Read(buffer, 5, 2));
                Throws<ArgumentException>(() => stream.Read(buffer, Int32.MaxValue, 1));
                Throws<ArgumentOutOfRangeException>(() => stream.Seek(0, (SeekOrigin)99));
                Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
                Equal(0, fake.ReadCalls); Equal(0, fake.SeekCalls); break;
            case "zero_count":
                Equal(0, stream.Read(buffer, buffer.Length, 0)); Equal(0, fake.ReadCalls); break;
            case "null_constructor":
                Throws<ArgumentNullException>(() => new ComStream(null)); break;
            case "read_only":
                if (stream.CanWrite) throw new Exception("Read-only wrapper advertises writing");
                Throws<NotSupportedException>(() => stream.Write(buffer, 0, 1));
                Throws<NotSupportedException>(() => stream.SetLength(0));
                stream.Flush(); Equal(0, fake.CommitCalls); Equal(4, stream.Length); break;
            case "managed_dispose":
                stream.Dispose(); stream.Dispose(); stream.Close(); break;
            case "disposed_contract":
                stream.Dispose();
                if (stream.CanRead || stream.CanSeek || stream.CanWrite) throw new Exception("Disposed capabilities remain enabled");
                Throws<ObjectDisposedException>(() => stream.Read(buffer, 0, 1));
                Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
                Throws<ObjectDisposedException>(() => { long x = stream.Position; });
                Throws<ObjectDisposedException>(() => { long x = stream.Length; });
                Throws<ObjectDisposedException>(() => stream.Flush()); break;
            case "native_shared":
                IntPtr sharedPointer = SHCreateMemStream(new byte[] { 1, 2, 3 }, 3);
                if (sharedPointer == IntPtr.Zero) throw new Exception("SHCreateMemStream failed");
                IStream first, second;
                try {
                    first = (IStream)Marshal.GetObjectForIUnknown(sharedPointer);
                    second = (IStream)Marshal.GetObjectForIUnknown(sharedPointer);
                } finally { Marshal.Release(sharedPointer); }
                var owner = new ComStream(first);
                owner.Dispose(); owner.Dispose(); owner.Close();
                try { STATSTG stat; second.Stat(out stat, 1); Equal(3, stat.cbSize); }
                finally { Marshal.ReleaseComObject(second); }
                break;
            case "native_dispose":
                for (int i = 0; i < 1000; ++i)
                {
                    IntPtr pointer = SHCreateMemStream(new byte[] { 1, 2, 3 }, 3);
                    if (pointer == IntPtr.Zero) throw new Exception("SHCreateMemStream failed");
                    IStream native;
                    try { native = (IStream)Marshal.GetObjectForIUnknown(pointer); }
                    finally { Marshal.Release(pointer); }
                    var adapter = new ComStream(native);
                    Equal(3, adapter.Read(buffer, 0, 6));
                    adapter.Dispose(); adapter.Dispose(); adapter.Close();
                    Throws<InvalidComObjectException>(() => native.Seek(0, 0, IntPtr.Zero));
                }
                break;
            case "zip_reader":
                using (var source = new MemoryStream())
                {
                    using (var zip = new System.IO.Compression.ZipArchive(source, System.IO.Compression.ZipArchiveMode.Create, true))
                    using (var output = zip.CreateEntry("fixture.txt").Open())
                    { output.Write(new byte[] { 1, 2, 3 }, 0, 3); }
                    using (var adapter = new ComStream(new FakeStream(source.ToArray())))
                    using (var zip = new System.IO.Compression.ZipArchive(adapter, System.IO.Compression.ZipArchiveMode.Read))
                    using (var input = zip.GetEntry("fixture.txt").Open())
                    { Equal(3, input.Read(buffer, 1, 3)); Equal(1, buffer[1]); Equal(3, buffer[3]); }
                }
                break;
            default: throw new Exception("Unknown test " + name);
        }
    }

    static int Main(string[] args)
    {
        try { Run(args[0]); Console.WriteLine("PASS " + args[0]); return 0; }
        catch (Exception e) { Console.WriteLine("FAIL " + args[0] + ": " + e); return 1; }
    }

    sealed class FakeStream : IStream
    {
        readonly MemoryStream data;
        public int MaximumRead = Int32.MaxValue;
        public int ReadCalls, SeekCalls, CommitCalls;
        public FakeStream(byte[] bytes) { data = new MemoryStream(bytes, false); }
        public void Read(byte[] buffer, int count, IntPtr read)
        {
            ReadCalls++;
            int actual = data.Read(buffer, 0, Math.Min(count, MaximumRead));
            if (read != IntPtr.Zero) Marshal.WriteInt32(read, actual);
        }
        public void Seek(long offset, int origin, IntPtr position)
        {
            SeekCalls++; long result = data.Seek(offset, (SeekOrigin)origin);
            if (position != IntPtr.Zero) Marshal.WriteInt64(position, result);
        }
        public void Stat(out STATSTG stat, int flags) { stat = new STATSTG(); stat.cbSize = data.Length; }
        public void Commit(int flags) { CommitCalls++; }
        public void Write(byte[] buffer, int count, IntPtr written) { throw new NotSupportedException(); }
        public void SetSize(long size) { throw new NotSupportedException(); }
        public void CopyTo(IStream target, long count, IntPtr read, IntPtr written) { throw new NotSupportedException(); }
        public void Revert() { throw new NotSupportedException(); }
        public void LockRegion(long offset, long count, int type) { throw new NotSupportedException(); }
        public void UnlockRegion(long offset, long count, int type) { throw new NotSupportedException(); }
        public void Clone(out IStream clone) { throw new NotSupportedException(); }
    }
}
