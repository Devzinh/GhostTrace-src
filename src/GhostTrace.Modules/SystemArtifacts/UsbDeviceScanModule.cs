using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using GhostTrace.Core.Abstractions;
using GhostTrace.Modules.Common;
using Microsoft.Win32;

namespace GhostTrace.Modules.SystemArtifacts;

/// <summary>
/// Enumerates USB mass-storage device history from USBSTOR — every removable drive ever
/// attached leaves a record here (device model, serial, friendly name). Relevant for
/// data-exfiltration and "evil maid" investigations (MITRE T1052 / T1091).
///
///   HKLM\SYSTEM\CurrentControlSet\Enum\USBSTOR\{device}\{serial}
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class UsbDeviceScanModule : IScanModule
{
    public string Name => "UsbDeviceScanModule";

    private const string UsbStorPath = @"SYSTEM\CurrentControlSet\Enum\USBSTOR";

    public Task<IScanResult> RunAsync(IScanContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var builder = new ScanResultBuilder(Name);

        using var usbstor = RegistryReader.OpenReadOnly(RegistryHive.LocalMachine, UsbStorPath);
        if (usbstor == null)
        {
            // No USB storage history is a valid state on a fresh machine.
            builder.SetMetadata("DevicesCollected", 0)
                   .SetMetadata("Note", "USBSTOR key absent (no removable storage history)");
            return Task.FromResult(builder.Build());
        }

        int devices = 0;

        foreach (var deviceName in usbstor.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var device = SafeOpen(usbstor, deviceName);
            if (device == null) continue;

            string model = PrettyDeviceName(deviceName);

            foreach (var serial in device.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var inst = SafeOpen(device, serial);
                if (inst == null) continue;

                string? friendly = RegistryReader.TryGetString(inst, "FriendlyName");
                DateTimeOffset? firstInstall = ReadInstallDate(inst);

                // USBSTOR serials ending in "&0" are interface ids, not real serials.
                string serialDisplay = serial.EndsWith("&0", StringComparison.Ordinal)
                    ? serial + " (interface-assigned, not a true serial)"
                    : serial;

                builder.AddFinding(
                    category: "UsbDevice",
                    description: friendly ?? model,
                    source: $"HKLM\\{UsbStorPath}\\{deviceName}\\{serial}",
                    timestampUtc: firstInstall,
                    rawValue: $"Model: {model} | Serial: {serialDisplay} | " +
                              $"FirstInstall: {firstInstall?.ToString("o") ?? "N/A"}");
                devices++;
            }
        }

        builder.SetMetadata("DevicesCollected", devices);
        return Task.FromResult(builder.Build());
    }

    /// <summary>
    /// Reads the device first-install timestamp from the Properties device-property store
    /// (DEVPKEY_Device_InstallDate, FILETIME). Best-effort — returns null if unavailable.
    /// </summary>
    private static DateTimeOffset? ReadInstallDate(RegistryKey instance)
    {
        // {83da6326-97a6-4088-9453-a1923f573b29}\0064 = InstallDate on Win8+.
        using var prop = SafeOpen(instance, @"Properties\{83da6326-97a6-4088-9453-a1923f573b29}\0064");
        if (prop == null) return null;
        var bytes = RegistryReader.TryGetBytes(prop, string.Empty);
        return bytes != null ? ForensicTime.FromFileTimeBytes(bytes) : null;
    }

    private static string PrettyDeviceName(string raw)
    {
        // e.g. "Disk&Ven_SanDisk&Prod_Cruzer_Blade&Rev_1.00"
        return raw.Replace("Ven_", "")
                  .Replace("Prod_", "")
                  .Replace("Rev_", "rev ")
                  .Replace("&", " ")
                  .Replace("_", " ")
                  .Trim();
    }

    private static RegistryKey? SafeOpen(RegistryKey parent, string name)
    {
        try { return parent.OpenSubKey(name, writable: false); }
        catch { return null; }
    }
}
