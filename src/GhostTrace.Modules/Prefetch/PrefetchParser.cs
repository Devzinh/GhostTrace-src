using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GhostTrace.Modules.Prefetch;

[SupportedOSPlatform("windows")]
public class PrefetchFormatException : Exception
{
    public PrefetchFormatException(string message) : base(message) { }
}

[SupportedOSPlatform("windows")]
public class UnsupportedPrefetchVersionException : Exception
{
    public UnsupportedPrefetchVersionException(string message) : base(message) { }
}

[SupportedOSPlatform("windows")]
public static class PrefetchParser
{
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern uint RtlDecompressBufferEx(
        ushort CompressionFormat,
        byte[] UncompressedBuffer,
        int UncompressedBufferSize,
        byte[] CompressedBuffer,
        int CompressedBufferSize,
        out int FinalUncompressedSize,
        IntPtr WorkSpace);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern uint RtlGetCompressionWorkSpaceSize(
        ushort CompressionFormatAndEngine,
        out int CompressBufferWorkSpaceSize,
        out int CompressFragmentWorkSpaceSize);

    private const ushort COMPRESSION_FORMAT_XPRESS_HUFF = 0x0004;
    private const uint MAM_HEADER = 0x044D414D; // MAM\x04 Little Endian
    private const uint VERSION_17 = 0x00000011; // WinXP / Vista / 7
    private const uint VERSION_23 = 0x00000017; // Win8 / 8.1
    private const uint VERSION_26 = 0x0000001A; // Win10
    private const uint VERSION_30 = 0x0000001E; // Win11 (initial)
    private const uint VERSION_31 = 0x0000001F; // Win11 23H2 / 24H2

    public static PrefetchEntry Parse(string filePath, ReadOnlySpan<byte> rawBytes)
    {
        if (rawBytes.Length < 8)
        {
            throw new PrefetchFormatException("File too small to contain a valid Prefetch/MAM header.");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(rawBytes);
        
        ReadOnlySpan<byte> bufferToParse;
        byte[]? decompressedArray = null;

        if (magic == MAM_HEADER)
        {
            int uncompressedSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(rawBytes.Slice(4));
            if (uncompressedSize <= 0 || uncompressedSize > 100 * 1024 * 1024) // Sanity check (max 100MB)
            {
                throw new PrefetchFormatException($"Invalid decompressed size in MAM header: {uncompressedSize}");
            }

            decompressedArray = new byte[uncompressedSize];
            byte[] compressedArray = rawBytes.Slice(8).ToArray();

            // XPRESS-Huffman decompression requires a scratch workspace; passing
            // IntPtr.Zero makes RtlDecompressBufferEx fail (STATUS_INVALID_PARAMETER),
            // which is why every modern (Win10/11) prefetch file failed to parse.
            uint wsStatus = RtlGetCompressionWorkSpaceSize(
                COMPRESSION_FORMAT_XPRESS_HUFF,
                out int workSpaceSize,
                out _);

            if (wsStatus != 0)
            {
                throw new PrefetchFormatException(
                    $"RtlGetCompressionWorkSpaceSize failed with NTSTATUS 0x{wsStatus:X8}");
            }

            IntPtr workSpace = Marshal.AllocHGlobal(workSpaceSize);
            try
            {
                uint ntstatus = RtlDecompressBufferEx(
                    COMPRESSION_FORMAT_XPRESS_HUFF,
                    decompressedArray,
                    decompressedArray.Length,
                    compressedArray,
                    compressedArray.Length,
                    out int finalSize,
                    workSpace);

                if (ntstatus != 0)
                {
                    throw new PrefetchFormatException($"RtlDecompressBufferEx failed with NTSTATUS 0x{ntstatus:X8}");
                }

                bufferToParse = decompressedArray.AsSpan(0, finalSize);
            }
            finally
            {
                Marshal.FreeHGlobal(workSpace);
            }
        }
        else
        {
            bufferToParse = rawBytes;
        }

        if (bufferToParse.Length < 84)
        {
            throw new PrefetchFormatException("Prefetch buffer too small after header detection.");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(bufferToParse);

        // A zero header means the .pf is locked / not yet flushed — this happens for the
        // prefetch files of currently-running system processes (dwm.exe, fontdrvhost.exe…)
        // that SysMain holds open. Treat it as "skip", not a parse failure.
        if (version == 0)
        {
            throw new UnsupportedPrefetchVersionException(
                "Prefetch header is zero — file is locked or not yet flushed (process likely running).");
        }
        if (version == VERSION_17 || version == VERSION_23)
        {
            throw new UnsupportedPrefetchVersionException(
                $"Version 0x{version:X} (WinXP/7/8) is not supported by this module.");
        }
        if (version != VERSION_26 && version != VERSION_30 && version != VERSION_31)
        {
            throw new PrefetchFormatException($"Unknown Prefetch version: 0x{version:X8}");
        }

        // Versions 30 and 31 (Windows 11) share the same layout for the fields we read.
        bool isWin11Layout = version == VERSION_30 || version == VERSION_31;

        uint signature = BinaryPrimitives.ReadUInt32LittleEndian(bufferToParse.Slice(4));
        if (signature != 0x41434353) // 'SCCA' in little endian
        {
            throw new PrefetchFormatException($"Invalid SCCA signature: 0x{signature:X8}");
        }

        // Extracted directly from file name as requested
        string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
        string hashFromPath = "Unknown";
        int dashIndex = fileName.LastIndexOf('-');
        if (dashIndex > 0 && dashIndex < fileName.Length - 1)
        {
            hashFromPath = fileName.Substring(dashIndex + 1);
            fileName = fileName.Substring(0, dashIndex);
        }
        
        // Ensure bounds before reading offsets based on version
        int runCountOffset = isWin11Layout ? 0xD0 : 0xC8;
        int runTimesOffset = isWin11Layout ? 0x80 : 0x78;

        if (bufferToParse.Length < runCountOffset + 4 || bufferToParse.Length < runTimesOffset + (8 * 8))
        {
            throw new PrefetchFormatException("Prefetch buffer too small to extract RunCount or RunTimes.");
        }

        int runCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(bufferToParse.Slice(runCountOffset));

        var runTimes = new List<DateTimeOffset>();
        for (int i = 0; i < 8; i++)
        {
            long ft = BinaryPrimitives.ReadInt64LittleEndian(bufferToParse.Slice(runTimesOffset + (i * 8)));
            if (ft > 0)
            {
                try
                {
                    runTimes.Add(DateTimeOffset.FromFileTime(ft).ToUniversalTime());
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Invalid FILETIME, ignore
                }
            }
        }

        DateTimeOffset? lastRunTimeUtc = null;
        if (runTimes.Count > 0)
        {
            lastRunTimeUtc = runTimes[0];
            foreach (var rt in runTimes)
            {
                if (rt > lastRunTimeUtc) lastRunTimeUtc = rt;
            }
        }

        return new PrefetchEntry(
            FileName: fileName,
            PrefetchHash: hashFromPath,
            RunCount: runCount,
            LastRunTimeUtc: lastRunTimeUtc,
            AllRunTimesUtc: runTimes.Count > 0 ? runTimes.ToArray() : null,
            FileVersion: version switch
            {
                VERSION_31 => 31,
                VERSION_30 => 30,
                _ => 26
            }
        );
    }
}
