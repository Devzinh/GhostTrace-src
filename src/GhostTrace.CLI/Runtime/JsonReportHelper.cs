using GhostTrace.Core.Abstractions;
using GhostTrace.Core.Models;
using GhostTrace.Core.Reporting;

namespace GhostTrace.CLI.Runtime;

/// <summary>
/// A minimal helper to centralize the boilerplate of writing a JSON report securely.
/// </summary>
internal static class JsonReportHelper
{
    public static async Task<bool> TryWriteReportAsync(
        FileInfo outputInfo,
        ReportDescriptor descriptor,
        IReadOnlyList<IScanResult> results)
    {
        if (!string.Equals(outputInfo.Extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[ERROR] Invalid file extension: '{outputInfo.Extension}'. The output file must be a .json file.");
            return false;
        }

        var writer = new JsonReportWriter();

        try
        {
            if (outputInfo.Directory != null && !outputInfo.Directory.Exists)
            {
                outputInfo.Directory.Create();
            }

            var fileStreamOptions = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous
            };

            await using var stream = new FileStream(outputInfo.FullName, fileStreamOptions);
            await writer.WriteAsync(descriptor, results, stream, CancellationToken.None);

            Console.WriteLine($"[SUCCESS] Report successfully generated at:");
            Console.WriteLine($"          {outputInfo.FullName}");
            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[ERROR] I/O error writing report to '{outputInfo.FullName}': {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[ERROR] Access denied writing report to '{outputInfo.FullName}': {ex.Message}");
            return false;
        }
        catch (System.Security.SecurityException ex)
        {
            Console.WriteLine($"[ERROR] Security error writing report to '{outputInfo.FullName}': {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> TryWritePayloadAsync<T>(
        FileInfo outputInfo,
        ReportDescriptor descriptor,
        T payload)
    {
        if (!string.Equals(outputInfo.Extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[ERROR] Invalid file extension: '{outputInfo.Extension}'. The output file must be a .json file.");
            return false;
        }

        var writer = new JsonReportWriter();

        try
        {
            if (outputInfo.Directory != null && !outputInfo.Directory.Exists)
            {
                outputInfo.Directory.Create();
            }

            var fileStreamOptions = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous
            };

            await using var stream = new FileStream(outputInfo.FullName, fileStreamOptions);
            await writer.WritePayloadAsync(descriptor, payload, stream, CancellationToken.None);

            Console.WriteLine($"[SUCCESS] Report successfully generated at:");
            Console.WriteLine($"          {outputInfo.FullName}");
            return true;
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[ERROR] I/O error writing report to '{outputInfo.FullName}': {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"[ERROR] Access denied writing report to '{outputInfo.FullName}': {ex.Message}");
            return false;
        }
        catch (System.Security.SecurityException ex)
        {
            Console.WriteLine($"[ERROR] Security error writing report to '{outputInfo.FullName}': {ex.Message}");
            return false;
        }
    }
}
