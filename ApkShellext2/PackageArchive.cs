using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace ApkShellext2 {
    // Readers own their input. A single close gate also protects COM-backed streams
    // when ZipFile itself closes a stream after a constructor failure.
    internal static class PackageArchive {
        internal const int MetadataLimit = 16 * 1024 * 1024;
        internal const int ResourcesLimit = 128 * 1024 * 1024;
        internal const int NestedPackageLimit = 512 * 1024 * 1024;

        internal static FileStream OpenFile(string path) {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        internal static ZipFile Open(Stream stream, Action<ZipFile> initialize) {
            if (stream == null) throw new ArgumentNullException("stream");
            var owned = new OwnedStream(stream);
            ZipFile archive = null;
            try {
                archive = new ZipFile(owned);
                initialize(archive);
                return archive;
            } catch {
                try { if (archive != null) archive.Close(); else owned.Dispose(); } catch { }
                throw;
            }
        }

        internal static byte[] ReadEntry(ZipFile archive, string name, int maxBytes, bool required = true) {
            var entry = archive.GetEntry(name);
            if (entry == null) {
                if (!required) return null;
                throw new InvalidDataException("Missing package entry: " + name);
            }
            if (entry.Size < 0 || entry.Size > maxBytes)
                throw new InvalidDataException("Package entry exceeds the supported size: " + name);
            using (var input = archive.GetInputStream(entry))
            using (var output = new MemoryStream()) {
                var buffer = new byte[8192];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0) {
                    if (output.Length > maxBytes - count)
                        throw new InvalidDataException("Package entry exceeds the supported size: " + name);
                    output.Write(buffer, 0, count);
                }
                if (output.Length != entry.Size) throw new InvalidDataException("Truncated package entry: " + name);
                return output.ToArray();
            }
        }

        private sealed class OwnedStream : Stream {
            private Stream inner;
            internal OwnedStream(Stream stream) { inner = stream; }
            public override bool CanRead { get { return inner != null && inner.CanRead; } }
            public override bool CanSeek { get { return inner != null && inner.CanSeek; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return inner.Length; } }
            public override long Position { get { return inner.Position; } set { inner.Position = value; } }
            public override int Read(byte[] buffer, int offset, int count) { return inner.Read(buffer, offset, count); }
            public override long Seek(long offset, SeekOrigin origin) { return inner.Seek(offset, origin); }
            public override void Flush() { }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing) {
                var stream = inner;
                inner = null;
                if (stream != null && disposing) stream.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
