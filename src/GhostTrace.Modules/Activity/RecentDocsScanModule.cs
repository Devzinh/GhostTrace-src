using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.Activity;

/// <summary>
/// Recovers RecentDocs — the per-user list of recently opened files/folders maintained
/// by Explorer, grouped by extension. Each value's data begins with the item name as a
/// null-terminated UTF-16 string. Strong evidence of file access / user activity.
///
///   HKU\{SID}\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RecentDocsScanModule : IScanModule
{
    public string Name => "RecentDocsScanModule";

    private const string RecentDocsSubPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs";

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        int docs = 0;

        using var users = RegistryReader.OpenReadOnly(RegistryHive.Users, string.Empty);
        if (users == null)
        {
            builder.AddError("Unable to open HKEY_USERS.").ForceStatus(Core.Enums.ScanStatus.Failure);
            return Task.FromResult(builder.Build());
        }

        foreach (var sid in users.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sid.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase)) continue;

            using var root = SafeOpen(users, $@"{sid}\{RecentDocsSubPath}");
            if (root == null) continue;

            // The root key plus one subkey per file extension (.exe, .docx, …).
            CollectFromKey(root, sid, "RecentDocs", builder, ref docs, cancellationToken);
            foreach (var ext in root.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var extKey = SafeOpen(root, ext);
                if (extKey != null) CollectFromKey(extKey, sid, $"RecentDocs\\{ext}", builder, ref docs, cancellationToken);
            }
        }

        builder.SetMetadata("DocsCollected", docs);
        return Task.FromResult(builder.Build());
    }

    private static void CollectFromKey(RegistryKey key, string sid, string label, ScanResultBuilder builder, ref int docs, CancellationToken ct)
    {
        foreach (var valueName in key.GetValueNames())
        {
            ct.ThrowIfCancellationRequested();
            // MRUListEx is an ordering blob, not a document.
            if (string.IsNullOrEmpty(valueName) || valueName.Equals("MRUListEx", StringComparison.OrdinalIgnoreCase))
                continue;

            var data = RegistryReader.TryGetBytes(key, valueName);
            string? itemName = BinaryText.ReadLeadingUtf16(data);
            if (string.IsNullOrEmpty(itemName)) continue;

            builder.AddFinding(
                category: "RecentDoc",
                description: itemName!,
                source: $"HKU\\{sid}\\...\\{label}",
                timestampUtc: null,
                rawValue: $"Recently opened: {itemName}");
            docs++;
        }
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string sub)
    {
        try { return parent.OpenSubKey(sub, writable: false); }
        catch { return null; }
    }
}
