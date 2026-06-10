using System;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace GhostTrace.Modules.Common;

/// <summary>
/// Formats raw registry values into a compact single-line representation suitable for
/// a forensic finding's RawValue. Shared by every module that reads the registry
/// (previously duplicated in RegistryScanModule and PersistenceScanModule).
/// </summary>
[SupportedOSPlatform("windows")]
public static class RegistryValueFormatter
{
    public static string Format(RegistryValueKind kind, object? data)
    {
        if (data == null) return "<null>";

        return kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => data.ToString() ?? string.Empty,
            RegistryValueKind.MultiString when data is string[] arr => string.Join(" \\0 ", arr),
            RegistryValueKind.DWord when data is int i => $"0x{i:X8} ({i})",
            RegistryValueKind.QWord when data is long l => $"0x{l:X16} ({l})",
            RegistryValueKind.Binary when data is byte[] b => FormatBinary(b),
            _ => $"[{data.GetType().Name}] {data}"
        };
    }

    /// <summary>Hex-encodes a byte array, truncating very large blobs for readability.</summary>
    public static string FormatBinary(byte[] bytes, int maxBytes = 256)
    {
        if (bytes.Length == 0) return "<empty>";
        int take = Math.Min(bytes.Length, maxBytes);
        var hex = BitConverter.ToString(bytes, 0, take).Replace("-", "");
        return take < bytes.Length ? $"{hex}… ({bytes.Length} bytes)" : hex;
    }

    /// <summary>Display name for a value, mapping the empty name to "(Default)".</summary>
    public static string DisplayName(string valueName) =>
        string.IsNullOrEmpty(valueName) ? "(Default)" : valueName;
}
