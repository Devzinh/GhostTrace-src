namespace GhostTrace.Core.Enums;

/// <summary>
/// Classifies the kind of scan target being inspected.
/// </summary>
public enum ScanTargetKind
{
    /// <summary>A file system path (file or directory).</summary>
    FileSystem = 0,

    /// <summary>A Windows registry hive or key path.</summary>
    Registry = 1,

    /// <summary>A running or snapshot process.</summary>
    Process = 2,

    /// <summary>A Windows event log channel.</summary>
    EventLog = 3,

    /// <summary>A WMI namespace or class.</summary>
    Wmi = 4,

    /// <summary>
    /// Escape hatch for application-defined targets not covered by existing members.
    /// This value is intended for exceptional or exploratory use only.
    /// If a custom target recurs across modules, promote it to a named enum member
    /// to preserve type safety and auditability.
    /// </summary>
    Custom = 255
}
