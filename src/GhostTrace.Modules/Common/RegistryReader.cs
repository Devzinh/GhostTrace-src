using System;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace GhostTrace.Modules.Common;

/// <summary>
/// Thin, strictly read-only helpers over <see cref="RegistryKey"/>. All openers default
/// to the 64-bit view so a 32-bit-built scanner still sees the real system hives, and
/// never request write access.
/// </summary>
[SupportedOSPlatform("windows")]
public static class RegistryReader
{
    /// <summary>
    /// Opens <paramref name="subPath"/> under <paramref name="hive"/> read-only.
    /// Returns <c>null</c> when the key is absent or access is denied (the caller decides
    /// whether that is an error or an expected condition).
    /// </summary>
    public static RegistryKey? OpenReadOnly(RegistryHive hive, string subPath, RegistryView view = RegistryView.Registry64)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            return baseKey.OpenSubKey(subPath, writable: false);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException
                                      or UnauthorizedAccessException
                                      or System.IO.IOException)
        {
            return null;
        }
    }

    public static string? TryGetString(RegistryKey key, string valueName)
    {
        try
        {
            return key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException
                       or UnauthorizedAccessException
                       or System.IO.IOException
                       or ObjectDisposedException)
        {
            return null;
        }
    }

    public static int? TryGetInt(RegistryKey key, string valueName)
    {
        try
        {
            return key.GetValue(valueName) is int i ? i : (int?)null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException
                       or UnauthorizedAccessException
                       or System.IO.IOException
                       or ObjectDisposedException)
        {
            return null;
        }
    }

    public static byte[]? TryGetBytes(RegistryKey key, string valueName)
    {
        try
        {
            return key.GetValue(valueName) as byte[];
        }
        catch (Exception ex) when (ex is System.Security.SecurityException
                       or UnauthorizedAccessException
                       or System.IO.IOException
                       or ObjectDisposedException)
        {
            return null;
        }
    }
}
