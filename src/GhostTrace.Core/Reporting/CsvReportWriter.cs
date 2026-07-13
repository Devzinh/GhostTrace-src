using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using GhostTrace.Core.Models;
using GhostTrace.Core.Reports;

namespace GhostTrace.Core.Reporting;

[SupportedOSPlatform("windows")]
public static class CsvReportWriter
{
    public static void Write(FullScanReport report, string outputPath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Module,Category,Description,Source,TimestampUtc,RawValue,IsMatch,Status");

        foreach (var m in report.ModuleResults)
        {
            var matchSet = new HashSet<ScanFinding>(m.MatchedFindings);

            var allFindings = new List<ScanFinding>(m.Findings.Count + m.MatchedFindings.Count);
            allFindings.AddRange(m.MatchedFindings);
            allFindings.AddRange(m.Findings);

            foreach (var f in allFindings)
            {
                bool isMatch = matchSet.Contains(f);
                string timestampStr = f.TimestampUtc?.ToString("o") ?? string.Empty;

                sb.AppendLine($"{CsvEscape(m.ModuleName)},{CsvEscape(f.Category)},{CsvEscape(f.Description)},{CsvEscape(f.Source)},{CsvEscape(timestampStr)},{CsvEscape(f.RawValue)},{isMatch.ToString().ToLowerInvariant()},{m.Status}");
            }
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes)
            return value;

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
