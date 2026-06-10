using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using GhostTrace.Core.Localization;
using GhostTrace.Core.Models;
using GhostTrace.Core.Reports;
using Microsoft.Win32;

namespace GhostTrace.CLI.Runtime;

/// <summary>
/// Performs the in-app, opt-in removal of the leftover traces a hunt found. Deletion is
/// done directly in .NET (no generated script): filesystem entries via System.IO and
/// Run-key values via the registry API. Every operation is existence-checked so it is
/// idempotent and never throws on already-gone items.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class TraceCleaner
{
    // Only categories that represent a concrete, safely-removable leftover.
    private static readonly HashSet<string> RemovableCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "FilesystemTrace",        // leftover files / folders on disk (find_trash core)
        "File",
        "PersistenceStartupFile", // Startup-folder entries
        "PersistenceRegistryRun", // Run / RunOnce values
    };

    public enum CleanupStatus { Removed, Skipped, Error }

    public sealed record RemovableTrace(string Category, string Target, string ValueName)
    {
        public bool IsRegistry => string.Equals(Category, "PersistenceRegistryRun", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when the target is an existing on-disk directory (vs a file).</summary>
        public bool IsDirectory => !IsRegistry && SafeIsDirectory(Target);

        public string Display => IsRegistry ? $"{Target} :: {ValueName}" : Target;
    }

    public sealed record CleanupOutcome(CleanupStatus Status, string Message);

    /// <summary>Extracts the deduplicated set of removable traces from the matched findings.</summary>
    public static IReadOnlyList<RemovableTrace> Collect(IEnumerable<ModuleScanResult> moduleResults)
    {
        var list = new List<RemovableTrace>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in moduleResults)
        {
            foreach (var f in module.MatchedFindings)
            {
                if (!RemovableCategories.Contains(f.Category)) continue;

                string valueName = string.Equals(f.Category, "PersistenceRegistryRun", StringComparison.OrdinalIgnoreCase)
                    ? f.Description
                    : string.Empty;

                string dedupeKey = $"{f.Category}|{f.Source}|{valueName}";
                if (seen.Add(dedupeKey))
                {
                    list.Add(new RemovableTrace(f.Category, f.Source, valueName));
                }
            }
        }

        return list;
    }

    /// <summary>Removes a single trace, returning the structured outcome (never throws).</summary>
    public static CleanupOutcome Remove(RemovableTrace trace)
    {
        try
        {
            return trace.IsRegistry ? RemoveRegistryValue(trace) : RemoveFilesystem(trace.Target);
        }
        catch (Exception ex)
        {
            return new CleanupOutcome(CleanupStatus.Error, $"{trace.Display} -> {ex.Message}");
        }
    }

    private static CleanupOutcome RemoveFilesystem(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            return new CleanupOutcome(CleanupStatus.Removed, path);
        }
        if (File.Exists(path))
        {
            // Clear read-only attribute so the delete can't be blocked by it.
            try { File.SetAttributes(path, FileAttributes.Normal); } catch { /* best effort */ }
            File.Delete(path);
            return new CleanupOutcome(CleanupStatus.Removed, path);
        }
        return new CleanupOutcome(CleanupStatus.Skipped, string.Format(Loc.Current.NotFoundFmt, path));
    }

    private static CleanupOutcome RemoveRegistryValue(RemovableTrace trace)
    {
        int sep = trace.Target.IndexOf('\\');
        if (sep <= 0) return new CleanupOutcome(CleanupStatus.Error, string.Format(Loc.Current.InvalidRegPathFmt, trace.Target));

        string hiveName = trace.Target.Substring(0, sep);
        string subPath = trace.Target.Substring(sep + 1);

        RegistryHive? hive = hiveName.ToLowerInvariant() switch
        {
            "currentuser" or "hkcu" or "hkey_current_user" => RegistryHive.CurrentUser,
            "localmachine" or "hklm" or "hkey_local_machine" => RegistryHive.LocalMachine,
            _ => null
        };
        if (hive is null) return new CleanupOutcome(CleanupStatus.Error, string.Format(Loc.Current.UnsupportedHiveFmt, hiveName));

        using var baseKey = RegistryKey.OpenBaseKey(hive.Value, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(subPath, writable: true);
        if (key is null) return new CleanupOutcome(CleanupStatus.Skipped, string.Format(Loc.Current.KeyNotFoundFmt, trace.Target));

        string valueName = trace.ValueName == "(Default)" ? string.Empty : trace.ValueName;
        if (key.GetValue(valueName) is null)
            return new CleanupOutcome(CleanupStatus.Skipped, string.Format(Loc.Current.ValueNotFoundFmt, trace.Display));

        key.DeleteValue(valueName, throwOnMissingValue: false);
        return new CleanupOutcome(CleanupStatus.Removed, trace.Display);
    }

    private static bool SafeIsDirectory(string path)
    {
        try { return Directory.Exists(path); }
        catch { return false; }
    }
}
