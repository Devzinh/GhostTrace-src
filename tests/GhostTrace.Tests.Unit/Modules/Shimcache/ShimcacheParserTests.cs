using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text;
using GhostTrace.Modules.Shimcache;
using Xunit;

namespace GhostTrace.Tests.Unit.Modules.Shimcache;

[SupportedOSPlatform("windows")]
public class ShimcacheParserTests
{
    // Builds a synthetic Windows 10/11 AppCompatCache blob with a 0x34 header
    // and one "10ts" record per supplied (path, fileTime) pair.
    private static byte[] BuildWin10Blob(IEnumerable<(string Path, long FileTime)> records)
    {
        var buffer = new List<byte>();

        // Header: first DWORD = offset to first record (0x34), padded with zeros.
        const int headerSize = 0x34;
        buffer.AddRange(BitConverter.GetBytes((uint)headerSize));
        while (buffer.Count < headerSize) buffer.Add(0);

        foreach (var (path, fileTime) in records)
        {
            byte[] pathBytes = Encoding.Unicode.GetBytes(path);
            ushort pathSize = (ushort)pathBytes.Length;
            const uint dataSize = 0;

            // CacheEntrySize counts everything after the size field:
            //   pathSize(2) + path + fileTime(8) + dataSize(4) + data(0)
            uint cacheEntrySize = (uint)(2 + pathBytes.Length + 8 + 4 + (int)dataSize);

            buffer.AddRange(BitConverter.GetBytes(0x73743031u)); // "10ts"
            buffer.AddRange(BitConverter.GetBytes(0u));          // unknown
            buffer.AddRange(BitConverter.GetBytes(cacheEntrySize));
            buffer.AddRange(BitConverter.GetBytes(pathSize));
            buffer.AddRange(pathBytes);
            buffer.AddRange(BitConverter.GetBytes(fileTime));
            buffer.AddRange(BitConverter.GetBytes(dataSize));
        }

        return buffer.ToArray();
    }

    [Fact]
    public void Parse_Win10Blob_ExtractsCleanPaths()
    {
        long ft = DateTime.UtcNow.ToFileTimeUtc();
        var blob = BuildWin10Blob(new[]
        {
            (@"C:\Windows\System32\cmd.exe", ft),
            (@"C:\Users\test\Downloads\app.exe", ft),
        });

        var entries = ShimcacheParser.Parse(blob);

        Assert.Equal(2, entries.Count);
        Assert.Equal(@"C:\Windows\System32\cmd.exe", entries[0].ExecutablePath);
        Assert.Equal(@"C:\Users\test\Downloads\app.exe", entries[1].ExecutablePath);
        Assert.Equal(1, entries[0].Index);
        Assert.Equal(2, entries[1].Index);
        // No mojibake — paths must not contain control / replacement characters.
        Assert.DoesNotContain('�', entries[0].ExecutablePath);
    }

    [Fact]
    public void Parse_ResolvesFileTimeToUtc()
    {
        var expected = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
        long ft = expected.ToFileTime();

        var blob = BuildWin10Blob(new[] { (@"C:\a.exe", ft) });
        var entries = ShimcacheParser.Parse(blob);

        Assert.Single(entries);
        Assert.NotNull(entries[0].LastModifiedUtc);
        Assert.Equal(expected, entries[0].LastModifiedUtc!.Value);
    }

    [Fact]
    public void Parse_ZeroFileTime_LeavesTimestampNull()
    {
        var blob = BuildWin10Blob(new[] { (@"C:\a.exe", 0L) });
        var entries = ShimcacheParser.Parse(blob);

        Assert.Single(entries);
        Assert.Null(entries[0].LastModifiedUtc);
        Assert.Equal(0, entries[0].FileTimeRaw);
    }

    [Fact]
    public void Parse_StopsAtPaddingAfterLastRecord()
    {
        long ft = DateTime.UtcNow.ToFileTimeUtc();
        var blob = new List<byte>(BuildWin10Blob(new[] { (@"C:\only.exe", ft) }));
        // Trailing zero padding (no further "10ts" signature) must not produce entries.
        blob.AddRange(new byte[64]);

        var entries = ShimcacheParser.Parse(blob.ToArray());

        Assert.Single(entries);
        Assert.Equal(@"C:\only.exe", entries[0].ExecutablePath);
    }

    [Fact]
    public void Parse_TooSmall_Throws()
    {
        Assert.Throws<ShimcacheFormatException>(() => ShimcacheParser.Parse(new byte[] { 0x01, 0x02 }));
    }

    [Fact]
    public void Parse_UnsupportedHeader_Throws()
    {
        // Header offset 0x10 is not a valid Win8.1/10/11 AppCompatCache header.
        var bad = new byte[64];
        BinaryPrimitives.WriteUInt32LittleEndian(bad, 0x10);
        Assert.Throws<ShimcacheFormatException>(() => ShimcacheParser.Parse(bad));
    }

    [Fact]
    public void Parse_CorruptPathSize_DoesNotOverrun()
    {
        // Header + a record claiming a huge path size must bail out safely, not throw/overrun.
        var buffer = new List<byte>();
        buffer.AddRange(BitConverter.GetBytes(0x34u));
        while (buffer.Count < 0x34) buffer.Add(0);
        buffer.AddRange(BitConverter.GetBytes(0x73743031u)); // "10ts"
        buffer.AddRange(BitConverter.GetBytes(0u));          // unknown
        buffer.AddRange(BitConverter.GetBytes(0x1000u));     // cacheEntrySize (bogus)
        buffer.AddRange(BitConverter.GetBytes((ushort)0xFFFF)); // pathSize way past end

        var entries = ShimcacheParser.Parse(buffer.ToArray());
        Assert.Empty(entries);
    }
}
