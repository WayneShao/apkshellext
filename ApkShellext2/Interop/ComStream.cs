using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace SharpShell.Helpers
{
    /// <summary>
    /// Read-only adapter for the Shell's package stream. Takes ownership of one
    /// RCW reference and releases it once on disposal. Managed IStream instances
    /// are borrowed. Like Stream, concurrent reading and disposal are unsupported.
    /// </summary>
    public class ComStream : Stream
    {
        private IStream comStream;
        private IntPtr bufferPointer;

        public ComStream(IStream comStream)
        {
            if (comStream == null) throw new ArgumentNullException("comStream");
            bufferPointer = Marshal.AllocCoTaskMem(8);
            this.comStream = comStream;
        }

        ~ComStream()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            IStream owned = Interlocked.Exchange(ref comStream, null);
            IntPtr pointer = Interlocked.Exchange(ref bufferPointer, IntPtr.Zero);
            try
            {
                if (pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pointer);
                // The RCW finalizer handles abandoned managed references. Avoid
                // explicit COM calls on the finalizer thread or on managed fakes.
                if (disposing && owned != null && Marshal.IsComObject(owned))
                    Marshal.ReleaseComObject(owned);
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private IStream GetStream()
        {
            if (comStream == null) throw new ObjectDisposedException("ComStream");
            return comStream;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            IStream source = GetStream();
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0) throw new ArgumentOutOfRangeException("offset");
            if (count < 0) throw new ArgumentOutOfRangeException("count");
            if (offset > buffer.Length || count > buffer.Length - offset)
                throw new ArgumentException("Offset and count exceed the buffer length.");
            if (count == 0) return 0;

            byte[] target = offset == 0 ? buffer : new byte[count];
            Marshal.WriteInt32(bufferPointer, 0);
            source.Read(target, count, bufferPointer);
            int actual = Marshal.ReadInt32(bufferPointer);
            if (actual < 0 || actual > count)
                throw new IOException("The COM stream returned an invalid byte count.");
            if (offset != 0) Buffer.BlockCopy(target, 0, buffer, offset, actual);
            return actual;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            IStream source = GetStream();
            if (origin < SeekOrigin.Begin || origin > SeekOrigin.End)
                throw new ArgumentOutOfRangeException("origin");
            if (origin == SeekOrigin.Begin && offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            source.Seek(offset, (int)origin, bufferPointer);
            return Marshal.ReadInt64(bufferPointer);
        }

        public override long Position
        {
            get { return Seek(0, SeekOrigin.Current); }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public override long Length
        {
            get
            {
                System.Runtime.InteropServices.ComTypes.STATSTG stat;
                GetStream().Stat(out stat, 1);
                return stat.cbSize;
            }
        }

        public override bool CanRead { get { return comStream != null; } }
        public override bool CanSeek { get { return comStream != null; } }
        public override bool CanWrite { get { return false; } }

        public override void Flush()
        {
            // A read-only package adapter has no changes to commit.
            GetStream();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            GetStream();
            throw new NotSupportedException("The package stream is read-only.");
        }

        public override void SetLength(long value)
        {
            GetStream();
            throw new NotSupportedException("The package stream is read-only.");
        }
    }
}
