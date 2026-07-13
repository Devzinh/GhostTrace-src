using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.SystemArtifacts;

/// <summary>
/// Name-aware filesystem trace search — the core Find_Trash behaviour. When a target
/// name is supplied (via <c>GetOption("targetName")</c>) it walks the common install /
/// leftover locations and reports any file or folder whose name contains that target,
/// without descending into already-matched folders (so a whole leftover directory is one
/// finding, not thousands). With no target it does nothing (full triage has no name).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FileSystemTraceScanModule : IScanModule
{
    public string Name => "FileSystemTraceScanModule";

    private const int MaxDepth = 4;
    private const int MaxMatches = 500;
    private const int MaxDirsVisited = 200_000;

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        string? target = context.GetOption("targetName");

        if (string.IsNullOrWhiteSpace(target))
        {
            builder.SetMetadata("Note", "No target name — filesystem trace search skipped (full triage).");
            return Task.FromResult(builder.Build());
        }

        int matches = 0;
        int dirsVisited = 0;

        foreach (var root in CommonRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (matches >= MaxMatches || dirsVisited >= MaxDirsVisited) break;
            if (!Directory.Exists(root)) continue;

            Walk(root, target, builder, ref matches, ref dirsVisited, cancellationToken);
        }

        builder.SetMetadata("MatchesFound", matches)
               .SetMetadata("DirectoriesVisited", dirsVisited);

        return Task.FromResult(builder.Build());
    }

    private void Walk(string root, string target, ScanResultBuilder builder, ref int matches, ref int dirsVisited, CancellationToken ct)
    {
        // Iterative BFS with explicit depth so a deep tree can't blow the stack.
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            if (matches >= MaxMatches || dirsVisited >= MaxDirsVisited) return;
            ct.ThrowIfCancellationRequested();

            var (dir, depth) = queue.Dequeue();
            dirsVisited++;

            // Files in this directory.
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    if (matches >= MaxMatches) return;
                    string fileName = Path.GetFileName(file);
                    if (fileName.Contains(target, StringComparison.OrdinalIgnoreCase))
                    {
                        // Same product-owned confidence rule as folders: only a file
                        // sitting directly in a curated root whose name exactly matches
                        // the target is removable. A name hit inside an unrelated
                        // shared path is a report-only hint.
                        bool highConfidence = depth == 0 &&
                            string.Equals(fileName, target, StringComparison.OrdinalIgnoreCase);

                        AddFileTrace(builder, file, highConfidence);
                        matches++;
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { /* skip locked dir */ }

            if (depth >= MaxDepth) continue;

            // Subdirectories: a matching folder is reported and NOT descended into.
            string[] subDirs;
            try { subDirs = Directory.GetDirectories(dir); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { continue; }

            foreach (var sub in subDirs)
            {
                if (matches >= MaxMatches) return;
                string folderName = Path.GetFileName(sub);

                if (folderName.Contains(target, StringComparison.OrdinalIgnoreCase))
                {
                    // Only a folder that sits directly under a curated root AND whose
                    // name exactly matches the target is a high-confidence product-owned
                    // leftover. Deeper or loose substring hits are report-only hints —
                    // a substring like "edge" must never mark a shared folder removable.
                    bool highConfidence = depth == 0 &&
                        string.Equals(folderName, target, StringComparison.OrdinalIgnoreCase);

                    AddFolderTrace(builder, sub, highConfidence);
                    matches++;
                    // Do not enqueue: the whole folder is the trace.
                }
                else
                {
                    queue.Enqueue((sub, depth + 1));
                }
            }
        }
    }

    private static void AddFileTrace(ScanResultBuilder builder, string file, bool highConfidence)
    {
        DateTimeOffset? modified = null;
        long size = 0;
        try { var fi = new FileInfo(file); modified = fi.LastWriteTimeUtc; size = fi.Length; } catch { }

        builder.AddFinding(
            category: highConfidence ? "FilesystemTrace" : "FilesystemTraceHint",
            description: file,
            source: file,
            timestampUtc: modified,
            rawValue: $"File | Size: {size} bytes | Modified: {modified?.ToString("o") ?? "N/A"}{(highConfidence ? "" : " | Hint: name substring match only (not removable)")}");
    }

    private static void AddFolderTrace(ScanResultBuilder builder, string folder, bool highConfidence)
    {
        DateTimeOffset? modified = null;
        try { modified = new DirectoryInfo(folder).LastWriteTimeUtc; } catch { }

        // "FilesystemTrace" is removable by the cleaner; "FilesystemTraceHint" is
        // informational only and never offered for deletion.
        builder.AddFinding(
            category: highConfidence ? "FilesystemTrace" : "FilesystemTraceHint",
            description: folder,
            source: folder,
            timestampUtc: modified,
            rawValue: $"Folder | Modified: {modified?.ToString("o") ?? "N/A"}{(highConfidence ? "" : " | Hint: name substring match only (not removable)")}");
    }

    private static IEnumerable<string> CommonRoots()
    {
        // System-wide install / data locations.
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); // ProgramData

        string sysDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\";
        string usersRoot = Path.Combine(sysDrive, "Users");

        // Per-user AppData (Local / Roaming / LocalLow) and Start Menu for every profile.
        if (Directory.Exists(usersRoot))
        {
            string[] profiles;
            try { profiles = Directory.GetDirectories(usersRoot); }
            catch { profiles = Array.Empty<string>(); }

            foreach (var profile in profiles)
            {
                yield return Path.Combine(profile, @"AppData\Local");
                yield return Path.Combine(profile, @"AppData\Roaming");
                yield return Path.Combine(profile, @"AppData\LocalLow");
            }
        }
    }
}
