using GhostTrace.Core.Enums;

namespace GhostTrace.Core.Abstractions;

/// <summary>
/// Represents a read-only scan target — the logical origin from which
/// an <see cref="IScanModule"/> collects forensic evidence.
/// A target is not limited to the file system; it may refer to a registry hive,
/// a process, an event log channel, or any other inspectable artifact.
/// </summary>
public interface IScanTarget
{
    /// <summary>
    /// Classifies the nature of this target (file system, registry, process, etc.).
    /// </summary>
    ScanTargetKind Kind { get; }

    /// <summary>
    /// Logical identifier or location of the target.
    /// The format depends on <see cref="Kind"/>:
    /// <list type="bullet">
    ///   <item><description><see cref="ScanTargetKind.FileSystem"/>: an absolute path.</description></item>
    ///   <item><description><see cref="ScanTargetKind.Registry"/>: a full registry key path.</description></item>
    ///   <item><description><see cref="ScanTargetKind.Process"/>: a process name or PID.</description></item>
    ///   <item><description><see cref="ScanTargetKind.EventLog"/>: a log channel name.</description></item>
    ///   <item><description><see cref="ScanTargetKind.Wmi"/>: a WMI namespace or class.</description></item>
    ///   <item><description><see cref="ScanTargetKind.Custom"/>: an application-defined identifier.</description></item>
    /// </list>
    /// </summary>
    string Identifier { get; }

    /// <summary>
    /// Optional human-readable label for display and reporting purposes.
    /// Returns <c>null</c> when no label has been assigned.
    /// </summary>
    string? DisplayName { get; }
}
