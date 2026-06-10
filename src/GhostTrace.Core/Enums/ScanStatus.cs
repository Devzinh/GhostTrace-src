namespace GhostTrace.Core.Enums;

/// <summary>
/// Indicates the outcome of a scan module execution.
/// </summary>
public enum ScanStatus
{
    /// <summary>The module completed and collected all expected evidence.</summary>
    Success = 0,

    /// <summary>The module completed but some evidence could not be collected.</summary>
    PartialSuccess = 1,

    /// <summary>The module failed and produced no usable evidence.</summary>
    Failure = 2,

    /// <summary>The module was intentionally skipped (e.g. no target configured in this run).</summary>
    Skipped = 3
}
