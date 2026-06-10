using System;
using System.Management;
using System.Runtime.Versioning;
using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Enums;
using GhostTrace.Modules.Common;

namespace GhostTrace.Modules.WmiPersistence;

/// <summary>
/// A read-only module that enumerates WMI persistence mechanisms (EventFilter,
/// EventConsumer, FilterToConsumerBinding) within the root\subscription namespace.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WmiPersistenceScanModule : IScanModule
{
    public string Name => "WmiPersistenceScanModule";

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new ScanResultBuilder(Name);
        int filters = 0, consumers = 0, bindings = 0;

        try
        {
            var options = new ConnectionOptions { EnablePrivileges = false };
            var scope = new ManagementScope(@"\\.\root\subscription", options);
            scope.Connect();

            var enumOptions = new System.Management.EnumerationOptions
            {
                Timeout = TimeSpan.FromSeconds(15),
                ReturnImmediately = true
            };

            EnumerateWmi(scope, "SELECT * FROM __EventFilter", enumOptions, builder, obj =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = GetStringOrNull(obj, "Name") ?? "Unknown";
                string query = GetStringOrNull(obj, "Query") ?? "Unknown";
                string ns = GetStringOrNull(obj, "EventNamespace") ?? "Unknown";
                builder.AddFinding("WmiPersistence", name, @"root\subscription\__EventFilter",
                    rawValue: $"Filter | Query: {query} | NS: {ns}");
                filters++;
            });

            EnumerateWmi(scope, "SELECT * FROM __EventConsumer", enumOptions, builder, obj =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = GetStringOrNull(obj, "Name") ?? "Unknown";
                string type = obj.ClassPath.ClassName ?? "Unknown";
                string action = type.ToLowerInvariant() switch
                {
                    "commandlineeventconsumer" => GetStringOrNull(obj, "CommandLineTemplate") ?? "Unknown",
                    "activescripteventconsumer" => GetStringOrNull(obj, "ScriptText") ?? "Unknown",
                    _ => "N/A"
                };
                var suspicion = SuspicionHeuristics.InspectCommand(action);
                string suffix = suspicion.Count > 0 ? $" | SUSPICIOUS: {string.Join(", ", suspicion)}" : "";
                builder.AddFinding("WmiPersistence", name, $@"root\subscription\{type}",
                    rawValue: $"Consumer:{type} | Action: {action}{suffix}");
                consumers++;
            });

            EnumerateWmi(scope, "SELECT * FROM __FilterToConsumerBinding", enumOptions, builder, obj =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                string normFilter = NormalizeWmiReference(GetStringOrNull(obj, "Filter") ?? "Unknown");
                string normConsumer = NormalizeWmiReference(GetStringOrNull(obj, "Consumer") ?? "Unknown");
                builder.AddFinding("WmiPersistence", $"{normFilter} -> {normConsumer}",
                    @"root\subscription\__FilterToConsumerBinding",
                    rawValue: $"Binding | Filter: {normFilter} | Consumer: {normConsumer}");
                bindings++;
            });
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or ManagementException)
        {
            builder.AddError($"WMI error accessing root\\subscription: {ex.Message}").ForceStatus(ScanStatus.Failure);
        }
        catch (Exception ex)
        {
            builder.AddError($"Unexpected error connecting to WMI: {ex.Message}").ForceStatus(ScanStatus.Failure);
        }

        builder.SetMetadata("FiltersFound", filters)
               .SetMetadata("ConsumersFound", consumers)
               .SetMetadata("BindingsFound", bindings);

        if (builder.FindingCount == 0 && builder.ErrorCount == 0)
        {
            builder.SetMetadata("Note", "No WMI persistence artifacts found (expected on clean systems)");
        }

        return Task.FromResult(builder.Build());
    }

    private void EnumerateWmi(ManagementScope scope, string queryStr, System.Management.EnumerationOptions options,
        ScanResultBuilder builder, Action<ManagementObject> processor)
    {
        try
        {
            var query = new ObjectQuery(queryStr);
            using var searcher = new ManagementObjectSearcher(scope, query, options);
            using var results = searcher.Get();
            foreach (var item in results)
            {
                using var mo = item as ManagementObject;
                if (mo != null)
                {
                    try { processor(mo); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { builder.AddError($"Failed to parse instance from ({queryStr}): {ex.Message}"); }
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            builder.AddError($"WMI query failed ({queryStr}): {ex.Message}");
        }
    }

    private string? GetStringOrNull(ManagementObject obj, string propertyName)
    {
        try { return obj[propertyName]?.ToString(); }
        catch { return null; }
    }

    private string NormalizeWmiReference(string reference)
    {
        int colonIndex = reference.IndexOf(':');
        return colonIndex >= 0 && colonIndex < reference.Length - 1
            ? reference.Substring(colonIndex + 1)
            : reference;
    }
}
