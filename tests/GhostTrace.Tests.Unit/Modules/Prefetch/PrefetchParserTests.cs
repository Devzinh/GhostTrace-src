using System;
using System.Buffers.Binary;
using System.Runtime.Versioning;
using GhostTrace.Modules.Prefetch;
using Xunit;

namespace GhostTrace.Tests.Unit.Modules.Prefetch;

[SupportedOSPlatform("windows")]
public class PrefetchParserTests
{
    // Builds a minimal *uncompressed* SCCA buffer (no MAM header) for a given version,
    // with one run time and a run count. The parser reads raw bytes directly when the
    // MAM magic is absent, so this exercises the version/offset logic without needing
    // to invoke ntdll decompression.
    private static byte[] BuildScca(uint version, long lastRunFileTime, int runCount)
    {
        bool win11 = version is 0x1E or 0x1F;
        int runTimesOffset = win11 ? 0x80 : 0x78;
        int runCountOffset = win11 ? 0xD0 : 0xC8;

        var buf = new byte[runCountOffset + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0), version);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4), 0x41434353); // 'SCCA'
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(runTimesOffset), lastRunFileTime);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(runCountOffset), (uint)runCount);
        return buf;
    }

    [Theory]
    [InlineData(0x1Au, 26)] // Win10
    [InlineData(0x1Eu, 30)] // Win11 initial
    [InlineData(0x1Fu, 31)] // Win11 23H2/24H2
    public void Parse_KnownVersions_AreAccepted(uint version, int expectedFriendly)
    {
        long ft = new DateTimeOffset(2025, 3, 1, 9, 0, 0, TimeSpan.Zero).ToFileTime();
        byte[] scca = BuildScca(version, ft, runCount: 7);

        var entry = PrefetchParser.Parse(@"C:\Windows\Prefetch\NOTEPAD.EXE-12345678.pf", scca);

        Assert.Equal(expectedFriendly, entry.FileVersion);
        Assert.Equal(7, entry.RunCount);
        Assert.Equal("NOTEPAD.EXE", entry.FileName);
        Assert.Equal("12345678", entry.PrefetchHash);
        Assert.NotNull(entry.LastRunTimeUtc);
    }

    [Fact]
    public void Parse_Version31_ResolvesLastRunTime()
    {
        var expected = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        byte[] scca = BuildScca(0x1F, expected.ToFileTime(), runCount: 1);

        var entry = PrefetchParser.Parse(@"C:\Windows\Prefetch\APP.EXE-ABCDEF01.pf", scca);

        Assert.NotNull(entry.LastRunTimeUtc);
        Assert.Equal(expected, entry.LastRunTimeUtc!.Value);
    }

    [Fact]
    public void Parse_LegacyVersion17_ThrowsUnsupported()
    {
        byte[] scca = BuildScca(0x11, 0, 0);
        Assert.Throws<UnsupportedPrefetchVersionException>(
            () => PrefetchParser.Parse(@"C:\x\OLD.EXE-1.pf", scca));
    }

    [Fact]
    public void Parse_UnknownVersion_ThrowsFormat()
    {
        byte[] scca = BuildScca(0x99, 0, 0);
        Assert.Throws<PrefetchFormatException>(
            () => PrefetchParser.Parse(@"C:\x\WEIRD.EXE-1.pf", scca));
    }

    [Fact]
    public void Parse_BadSignature_ThrowsFormat()
    {
        byte[] scca = BuildScca(0x1F, 0, 0);
        scca[4] = 0x00; // corrupt 'SCCA'
        Assert.Throws<PrefetchFormatException>(
            () => PrefetchParser.Parse(@"C:\x\APP.EXE-1.pf", scca));
    }
}
