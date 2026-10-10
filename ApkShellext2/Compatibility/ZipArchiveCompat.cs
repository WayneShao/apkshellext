using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ICSharpCode.SharpZipLib.Zip;

// Small source-compatible facade for the reader code. The implementation is the
// .NET 10 ZipArchive reader; no SharpZipLib code is shipped in the extension.
internal sealed class ZipEntry
{
    internal ZipArchiveEntry Inner { get; }
    public string Name => Inner.FullName;
    public long Size => Inner.Length;

    internal ZipEntry(ZipArchiveEntry inner) => Inner = inner;
}

internal sealed class ZipFile : IEnumerable<ZipEntry>, IDisposable
{
    private readonly ZipArchive archive;
    private readonly List<ZipEntry> entries;

    public ZipFile(Stream source)
    {
        archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);
        entries = archive.Entries.Select(e => new ZipEntry(e)).ToList();
    }

    public ZipEntry GetEntry(string name)
        => entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal))
           ?? entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    public int FindEntry(string name, bool ignoreCase)
    {
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var i = 0; i < entries.Count; i++)
            if (string.Equals(entries[i].Name, name, comparison)) return i;
        return -1;
    }

    public Stream GetInputStream(ZipEntry entry) => entry.Inner.Open();
    public void Close() => Dispose();
    public void Dispose() => archive.Dispose();
    public IEnumerator<ZipEntry> GetEnumerator() => entries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
