using System;
using System.Text;

namespace GhostTrace.Modules.Common;

/// <summary>
/// Helpers for extracting text embedded in binary registry values / artifact blobs.
/// </summary>
public static class BinaryText
{
    /// <summary>
    /// Reads the leading null-terminated UTF-16LE string from <paramref name="data"/>
    /// (the encoding Explorer uses for RecentDocs / shell MRU value names). Returns
    /// <c>null</c> when there is no valid leading string.
    /// </summary>
    public static string? ReadLeadingUtf16(byte[]? data)
    {
        if (data == null || data.Length < 2) return null;

        int end = 0;
        while (end + 1 < data.Length)
        {
            if (data[end] == 0 && data[end + 1] == 0) break;
            end += 2;
        }
        if (end == 0) return null;
        return Encoding.Unicode.GetString(data, 0, end);
    }
}
