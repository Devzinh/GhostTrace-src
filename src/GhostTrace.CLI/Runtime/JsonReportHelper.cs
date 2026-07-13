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
        IReadOnlyList<IScanResult> results,
        CancellationToken cancellationToken = default)
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

            await WriteAtomicallyAsync(
                outputInfo,
                (stream, token) => writer.WriteAsync(descriptor, results, stream, token),
                cancellationToken);

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
        T payload,
        CancellationToken cancellationToken = default)
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

            await WriteAtomicallyAsync(
                outputInfo,
                (stream, token) => writer.WritePayloadAsync(descriptor, payload, stream, token),
                cancellationToken);

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

    private static async Task WriteAtomicallyAsync(
        FileInfo outputInfo,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken)
    {
        string temporaryPath = outputInfo.FullName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var fileStreamOptions = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous
            };

            await using (var stream = new FileStream(temporaryPath, fileStreamOptions))
            {
                await writeAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, outputInfo.FullName, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
