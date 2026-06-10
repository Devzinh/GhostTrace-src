using System;
using System.Runtime.Versioning;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.Filesystem;

/// <summary>
/// A read-only module that enumerates files in a target directory without modifying
/// attributes, timestamps, or content.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FilesystemScanModule : IScanModule
{
    /// <inheritdoc />
    public string Name => "FilesystemScanModule";

    /// <inheritdoc />
    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        var targetPath = context.GetOption("targetPath");

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return Task.FromResult(builder
                .AddError("Required option 'targetPath' is missing or empty in context.")
                .ForceStatus(ScanStatus.Failure)
                .Build());
        }

        builder.SetMetadata("TargetPath", targetPath);

        var dirInfo = new DirectoryInfo(targetPath);
        if (!dirInfo.Exists)
        {
            return Task.FromResult(builder
                .AddError($"Target directory does not exist: {targetPath}")
                .ForceStatus(ScanStatus.Failure)
                .Build());
        }

        try
        {
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    builder.AddFinding(
                        category: "File",
                        description: file.Name,
                        source: file.FullName,
                        timestampUtc: file.LastWriteTimeUtc,
                        rawValue: $"Size: {file.Length} bytes | Created: {file.CreationTimeUtc:o} | Modified: {file.LastWriteTimeUtc:o}");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    builder.AddError($"Error reading metadata for file '{file.FullName}': {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            builder.AddError($"Error enumerating directory '{targetPath}': {ex.Message}");
        }

        builder.SetMetadata("FilesFound", builder.FindingCount);
        return Task.FromResult(builder.Build());
    }
}
