using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text;

namespace GhostTrace.Modules.Shimcache;

[SupportedOSPlatform("windows")]
public class ShimcacheFormatException : Exception
{
    public ShimcacheFormatException(string message) : base(message) { }
}

[SupportedOSPlatform("windows")]
public record ShimcacheEntry(
    int Index,
    string ExecutablePath,
    long FileTimeRaw,
    DateTimeOffset? LastModifiedUtc
);

/// <summary>
/// Parser for the Windows 8.1 / 10 / 11 AppCompatCache (Shimcache) binary blob
/// stored under HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatCache.
///
/// Layout (Windows 10/11):
///   Header
///     0x00  uint32  HeaderSize  (offset to the first record: 0x30 or 0x34)
///     ...   reserved
///   Records (repeated), each:
///     +0x00  char[4]  Signature        "10ts" (0x73743031 LE)
///     +0x04  uint32   Unknown          (sequence / crc)
///     +0x08  uint32   CacheEntrySize   (bytes that follow this field)
///     +0x0C  uint16   PathSize         (bytes, UTF-16LE)
///     +0x0E  byte[]   Path             (PathSize bytes)
///     +..    int64    LastModified     (FILETIME, UTC)
///     +..    uint32   DataSize
///     +..    byte[]   Data
///
/// Windows 8.1 records use the "10ts" signature too; older Win8.0 uses "00ts".
/// </summary>
[SupportedOSPlatform("windows")]
public static class ShimcacheParser
{
    // "10ts" little-endian — start-of-record signature on Win8.1/10/11.
    private const uint SignatureWin10 = 0x73743031;
    // "00ts" little-endian — Win8.0 records.
    private const uint SignatureWin80 = 0x73743030;

    private const int MaxReasonablePathBytes = 1024 * 2; // 1024 UTF-16 chars

    public static IReadOnlyList<ShimcacheEntry> Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
        {
            throw new ShimcacheFormatException("Data too small to contain a valid Shimcache header.");
        }

        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(data);

        // On Win8.1/10/11 the first DWORD is the offset to the first record.
        // Known good values are 0x30 (48) and 0x34 (52). Anything else is a
        // format we do not support (e.g. WinXP/7 used an entirely different layout).
        if (headerSize != 0x30 && headerSize != 0x34)
        {
            throw new ShimcacheFormatException(
                $"Unsupported Shimcache format (header offset 0x{headerSize:X}). " +
                "Only Windows 8.1/10/11 AppCompatCache is supported.");
        }

        if (headerSize > data.Length)
        {
            throw new ShimcacheFormatException("Header offset points beyond the end of the data.");
        }

        var entries = new List<ShimcacheEntry>();
        int offset = (int)headerSize;
        int index = 1;

        // Minimum record prologue: signature(4) + unknown(4) + cacheEntrySize(4) + pathSize(2)
        const int RecordPrologue = 14;

        while (offset + RecordPrologue <= data.Length)
        {
            uint signature = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
            if (signature != SignatureWin10 && signature != SignatureWin80)
            {
                // No more valid records (reached padding / end of meaningful data).
                break;
            }

            uint cacheEntrySize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 8));
            ushort pathSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset + 12));

            int pathStart = offset + 14;

            // Defensive bounds: a corrupt pathSize must not read past the buffer.
            if (pathSize == 0 || pathSize > MaxReasonablePathBytes || pathStart + pathSize > data.Length)
            {
                break;
            }

            string path = Encoding.Unicode.GetString(data.Slice(pathStart, pathSize));

            long fileTimeRaw = 0;
            DateTimeOffset? lastModifiedUtc = null;

            int afterPath = pathStart + pathSize;
            if (afterPath + 8 <= data.Length)
            {
                fileTimeRaw = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(afterPath, 8));
                if (fileTimeRaw > 0)
                {
                    try
                    {
                        lastModifiedUtc = DateTimeOffset.FromFileTime(fileTimeRaw).ToUniversalTime();
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Invalid FILETIME — keep the raw value, leave the parsed timestamp null.
                    }
                }
            }

            entries.Add(new ShimcacheEntry(index++, path, fileTimeRaw, lastModifiedUtc));

            // CacheEntrySize counts the bytes that follow it (from offset+12 onward),
            // so the next record begins at offset + 12 + CacheEntrySize.
            long next = (long)offset + 12 + cacheEntrySize;

            // Guard against zero/backward progress from a corrupt size field:
            // fall back to advancing just past the path we already read.
            if (cacheEntrySize == 0 || next <= offset)
            {
                long fallback = (long)afterPath + 8;
                if (fallback <= offset) break;
                next = fallback;
            }

            if (next > data.Length) break;
            offset = (int)next;
        }

        return entries;
    }
}
