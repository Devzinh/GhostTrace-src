using System.Text;
using GhostTrace.Modules.Common;
using Xunit;

namespace GhostTrace.Tests.Unit.Modules.Common;

public class BinaryTextTests
{
    [Fact]
    public void ReadLeadingUtf16_ParsesNullTerminatedString()
    {
        string expected = @"C:\Users\bob\report.docx";
        byte[] data = Encoding.Unicode.GetBytes(expected + "\0");

        Assert.Equal(expected, BinaryText.ReadLeadingUtf16(data));
    }

    [Fact]
    public void ReadLeadingUtf16_StopsAtFirstDoubleNull_IgnoringTrailingBlob()
    {
        // RecentDocs values carry a PIDL after the name — only the name must be read.
        byte[] name = Encoding.Unicode.GetBytes("photo.jpg\0");
        byte[] trailing = { 0x14, 0x00, 0x1F, 0x00, 0x44, 0x00 };
        byte[] data = new byte[name.Length + trailing.Length];
        name.CopyTo(data, 0);
        trailing.CopyTo(data, name.Length);

        Assert.Equal("photo.jpg", BinaryText.ReadLeadingUtf16(data));
    }

    [Fact]
    public void ReadLeadingUtf16_EmptyOrTooShort_ReturnsNull()
    {
        Assert.Null(BinaryText.ReadLeadingUtf16(null));
        Assert.Null(BinaryText.ReadLeadingUtf16(System.Array.Empty<byte>()));
        Assert.Null(BinaryText.ReadLeadingUtf16(new byte[] { 0x41 }));
    }

    [Fact]
    public void ReadLeadingUtf16_LeadingNull_ReturnsNull()
    {
        Assert.Null(BinaryText.ReadLeadingUtf16(new byte[] { 0x00, 0x00, 0x41, 0x00 }));
    }
}
