using System;

namespace GhostTrace.Modules.Common;

/// <summary>
/// Safe conversions for the various timestamp encodings found in Windows forensic
/// artifacts. All methods are total — invalid inputs return <c>null</c> rather than throwing.
/// </summary>
public static class ForensicTime
{
    /// <summary>
    /// Converts a Windows FILETIME (100-ns ticks since 1601-01-01 UTC) to a UTC
    /// <see cref="DateTimeOffset"/>. Returns <c>null</c> for zero or out-of-range values.
    /// </summary>
    public static DateTimeOffset? FromFileTimeUtc(long fileTime)
    {
        if (fileTime <= 0) return null;
        try
        {
            return DateTimeOffset.FromFileTime(fileTime).ToUniversalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a little-endian 8-byte FILETIME from the start of <paramref name="span"/>.
    /// Returns <c>null</c> when there are fewer than 8 bytes or the value is invalid.
    /// </summary>
    public static DateTimeOffset? FromFileTimeBytes(ReadOnlySpan<byte> span)
    {
        if (span.Length < 8) return null;
        long ft = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(span);
        return FromFileTimeUtc(ft);
    }

    /// <summary>
    /// Parses a compact "yyyyMMdd" date (used by the Uninstall registry InstallDate value)
    /// as a UTC date. Returns <c>null</c> when the input is empty or malformed.
    /// </summary>
    public static DateTimeOffset? FromCompactDate(string? yyyymmdd)
    {
        if (string.IsNullOrWhiteSpace(yyyymmdd)) return null;

        // Parse with Kind=Unspecified (no AssumeUniversal — that would convert to local
        // time and produce a non-zero offset, breaking the explicit TimeSpan.Zero below).
        if (System.DateTime.TryParseExact(
                yyyymmdd.Trim(), "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
        {
            return new DateTimeOffset(dt, TimeSpan.Zero);
        }
        return null;
    }

    /// <summary>
    /// Parses a Windows SYSTEMTIME (16 bytes: year, month, dayOfWeek, day, hour,
    /// minute, second, milliseconds — each a little-endian UInt16) to UTC.
    /// Used by NetworkList profile DateCreated / DateLastConnected values.
    /// </summary>
    public static DateTimeOffset? FromSystemTimeBytes(ReadOnlySpan<byte> span)
    {
        if (span.Length < 16) return null;

        int year = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(0, 2));
        int month = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(2, 2));
        // span[4..6] is dayOfWeek — derived, ignored.
        int day = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(6, 2));
        int hour = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(8, 2));
        int minute = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(10, 2));
        int second = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(12, 2));
        int millis = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(14, 2));

        if (year < 1601 || year > 9999 || month is < 1 or > 12 || day is < 1 or > 31 ||
            hour > 23 || minute > 59 || second > 59 || millis > 999)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, millis, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null; // e.g. day 31 in a 30-day month
        }
    }
}
