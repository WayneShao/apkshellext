using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ApkShellext2;

class ServiceRegression {
    static int checks;
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    sealed class CountingOutput : Stream {
        internal long Count;
        internal int LargestWrite;
        public override void Write(byte[] b, int offset, int count) { Count += count; LargestWrite = Math.Max(LargestWrite, count); }
        public override bool CanRead { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override long Length { get { return Count; } }
        public override long Position { get { return Count; } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override int Read(byte[] b, int o, int c) { throw new NotSupportedException(); }
        public override long Seek(long o, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
    }
    public static int Main() {
        string root = Path.Combine(Path.GetTempPath(), "ApkServiceTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            string path = Path.Combine(root, "sample.APK");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            string key = "0123456789abcdef0123456789abcdef";
            var shares = new PackageShareRegistry();
            Assert(!shares.TryRegister(IPAddress.Parse("192.0.2.10"), key, path), "remote registration accepted");
            Assert(!shares.TryRegister(null, key, path), "missing remote endpoint accepted");
            Assert(!shares.TryRegister(IPAddress.Loopback, "123456", path), "short key accepted");
            Assert(!shares.TryRegister(IPAddress.Loopback, new string('z', 32), path), "nonhex key accepted");
            Assert(!shares.TryRegister(IPAddress.Loopback, key, Path.Combine(root, "absent.apk")), "missing package accepted");
            string bad = Path.Combine(root, "private.txt"); File.WriteAllText(bad, "data");
            Assert(!shares.TryRegister(IPAddress.Loopback, key, bad), "unsupported extension accepted");
            string dir = Path.Combine(root, "directory.apk"); Directory.CreateDirectory(dir);
            Assert(!shares.TryRegister(IPAddress.Loopback, key, dir), "directory accepted");
            Assert(shares.TryRegister(IPAddress.Loopback, key, path), "local valid registration rejected");
            Assert(shares.Lookup(key.ToUpperInvariant()) == path, "case-insensitive lookup failed");
            Assert(shares.Lookup("../" + key) == "", "invalid download key accepted");
            Assert(shares.TryRegister(IPAddress.IPv6Loopback, new string('a', 32), path), "IPv6 loopback rejected");
            foreach (string ext in new[] { ".ipa", ".appx", ".appxbundle" }) {
                string p = Path.Combine(root, "sample" + ext); File.WriteAllBytes(p, new byte[0]);
                Assert(shares.TryRegister(IPAddress.Loopback, new string('b', 32), p), "supported extension rejected " + ext);
            }
            Parallel.For(0, 1000, i => {
                string id = i.ToString("x32");
                if (!shares.TryRegister(IPAddress.Loopback, id, path) || shares.Lookup(id) != path)
                    throw new Exception("Concurrent registry access failed");
            }); checks++;
            using (var reader = WebServer.OpenPackageRead(path)) {
                Assert(!reader.CanWrite && reader.CanRead, "package handle is not read-only");
                using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) {
                    writer.Seek(0, SeekOrigin.End); writer.WriteByte(4);
                }
                File.Move(path, path + ".moved"); File.Move(path + ".moved", path);
                Assert(reader.ReadByte() == 1, "shared handle lost after rename");
            }
            File.SetAttributes(path, FileAttributes.ReadOnly);
            using (var reader = WebServer.OpenPackageRead(path)) Assert(reader.ReadByte() == 1, "read-only package failed");
            File.SetAttributes(path, FileAttributes.Normal);
            using (var input = new MemoryStream(new byte[3 * 1024 * 1024]))
            using (var output = new CountingOutput()) {
                WebServer.CopyPackage(input, output, input.Length - 1);
                Assert(output.Count == input.Length - 1, "streamed beyond declared length");
                Assert(output.LargestWrite <= 64 * 1024, "copy buffer is not bounded");
            }
            bool truncated = false;
            try { using (var input = new MemoryStream(new byte[1])) using (var output = new CountingOutput()) WebServer.CopyPackage(input, output, 2); }
            catch (EndOfStreamException) { truncated = true; }
            Assert(truncated, "truncated transfer accepted");
            // Prefix collection validation does not bind a port or start a listener.
            using (var listener = new HttpListener()) {
                foreach (string prefix in WebServer.GetPrefixes("192.0.2.10")) {
                    Assert(prefix.EndsWith("/"), "listener prefix misses slash");
                    listener.Prefixes.Add(prefix);
                }
                Assert(!listener.IsListening, "test started listener");
            }
            Console.WriteLine("PASS service checks=" + checks); return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally {
            foreach (string file in Directory.GetFiles(root)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(root, true);
        }
    }
}
