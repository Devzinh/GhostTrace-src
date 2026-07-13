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

    /// <summary>
    /// Extracts the deduplicated set of removable traces from the matched findings.
    /// Only high-confidence findings qualify: the hunted name must appear in the
    /// artifact's own name (file/folder name or registry value name), not merely
    /// anywhere in the raw text — a generic substring hit is never a cleanup candidate.
    /// </summary>
    public static IReadOnlyList<RemovableTrace> Collect(IEnumerable<ModuleScanResult> moduleResults, string targetName)
    {
        var list = new List<RemovableTrace>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in moduleResults)
        {
            foreach (var f in module.MatchedFindings)
            {
                if (!RemovableCategories.Contains(f.Category)) continue;
                if (!IsHighConfidenceMatch(f, targetName)) continue;

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

    /// <summary>
    /// True when the hunted name appears in the artifact's own name — the file or
    /// folder name for filesystem traces, the value name (Description) for registry
    /// Run entries — rather than only somewhere in description/source/raw text.
    /// </summary>
    private static bool IsHighConfidenceMatch(ScanFinding f, string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return false;

        if (string.Equals(f.Category, "PersistenceRegistryRun", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrEmpty(f.Description) &&
                   f.Description.Contains(targetName, StringComparison.OrdinalIgnoreCase);
        }

        string name;
        try { name = Path.GetFileName(f.Source.TrimEnd('\\', '/')); }
        catch { return false; }
             return !string.IsNullOrEmpty(name) &&
                 string.Equals(name, targetName, StringComparison.OrdinalIgnoreCase);
    }

    private static CleanupOutcome RemoveFilesystem(string path)
    {
        if (Directory.Exists(path))
        {
            // Recursive deletion is only allowed for validated product-owned
            // locations (direct children of the curated leftover roots). Anything
            // else — system dirs, drive roots, nested shared folders — is blocked.
            if (!IsTrustedRemovableDirectory(path))
            {
                return new CleanupOutcome(CleanupStatus.Skipped, string.Format(Loc.Current.BlockedDirFmt, path));
            }
            if (IsReparsePoint(path))
            {
                return new CleanupOutcome(CleanupStatus.Skipped, string.Format(Loc.Current.BlockedDirFmt, path));
            }
            Directory.Delete(path, recursive: true);
            return new CleanupOutcome(CleanupStatus.Removed, path);
        }
        if (File.Exists(path))
        {
            // Files get the same ownership validation as directories: only files
            // sitting directly in a curated leftover root (or a Startup folder, for
            // persistence entries) may be deleted. A name match inside an unrelated
            // shared path is rejected.
            if (!IsTrustedRemovableFile(path))
            {
                return new CleanupOutcome(CleanupStatus.Skipped, string.Format(Loc.Current.BlockedDirFmt, path));
            }
            if (IsReparsePoint(path))
            {
                return new CleanupOutcome(CleanupStatus.Skipped, string.Format(Loc.Current.BlockedDirFmt, path));
            }
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

    /// <summary>
    /// Curated allowlist trust rule: a directory may only be deleted recursively when
    /// it is a direct child of one of the well-known per-product leftover roots
    /// (Program Files, Program Files (x86), ProgramData, or a user profile's
    /// AppData\Local / Roaming / LocalLow). The roots themselves, drive roots and any
    /// deeper or unrelated path are rejected.
    /// </summary>
    internal static bool IsTrustedRemovableDirectory(string path) => HasTrustedParent(path);

    /// <summary>
    /// Trust rule for single-file deletion: the file must sit directly in a curated
    /// leftover root, or in a Startup folder (the location Startup-persistence
    /// findings legitimately point at). Files anywhere else — shared or system
    /// directories that merely contain a name match — are rejected.
    /// </summary>
    internal static bool IsTrustedRemovableFile(string path)
    {
        if (HasTrustedParent(path)) return true;

        string? parent;
        try { parent = Path.GetDirectoryName(Path.GetFullPath(path)); }
        catch { return false; }
        if (string.IsNullOrEmpty(parent)) return false;

        // Per-user and common Startup folders (any profile).
        return parent.TrimEnd('\\').EndsWith(@"\Microsoft\Windows\Start Menu\Programs\Startup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTrustedParent(string path)
    {
        string full;
        try { full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch { return false; }

        string? parent;
        try { parent = Path.GetDirectoryName(full); }
        catch { return false; }
        if (string.IsNullOrEmpty(parent)) return false; // drive root or the root itself

        foreach (var root in TrustedLeftoverRoots())
        {
            if (string.IsNullOrEmpty(root)) continue;
            string normalizedRoot;
            try { normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)); }
            catch { continue; }

            if (string.Equals(parent, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static IEnumerable<string> TrustedLeftoverRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); // ProgramData

        string sysDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\";
        string usersRoot = Path.Combine(sysDrive, "Users");

        string[] profiles;
        try { profiles = Directory.Exists(usersRoot) ? Directory.GetDirectories(usersRoot) : Array.Empty<string>(); }
        catch { profiles = Array.Empty<string>(); }

        foreach (var profile in profiles)
        {
            yield return Path.Combine(profile, @"AppData\Local");
            yield return Path.Combine(profile, @"AppData\Roaming");
            yield return Path.Combine(profile, @"AppData\LocalLow");
        }
    }

    private static bool SafeIsDirectory(string path)
    {
        try { return Directory.Exists(path); }
        catch { return false; }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }
}
