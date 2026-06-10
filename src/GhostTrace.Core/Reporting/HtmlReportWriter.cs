using System;
using System.IO;
using System.Net;
using System.Runtime.Versioning;
using System.Text;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Reports;

namespace GhostTrace.Core.Reporting;

[SupportedOSPlatform("windows")]
public static class HtmlReportWriter
{
    public static void Write(FullScanReport report, string outputPath)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>GhostTrace Report — {WebUtility.HtmlEncode(report.MachineName)} — {report.ScanStartedAt.ToLocalTime():yyyy-MM-dd}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(@"
        body { background-color: #0f1117; color: #e2e8f0; font-family: 'Consolas', 'Courier New', monospace; margin: 0; padding: 2rem; }
        .container { max-width: 1200px; margin: 0 auto; }
        h1, h2, h3 { color: #e2e8f0; }
        .accent { color: #4f98a3; }
        .bg-accent { background-color: #4f98a3; color: #0f1117; }
        .card { background-color: #1e2130; border-radius: 8px; border: 1px solid #2d3748; padding: 1.5rem; margin-bottom: 1.5rem; }
        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; }
        .summary-item { display: flex; flex-direction: column; }
        .summary-label { color: #94a3b8; font-size: 0.875rem; margin-bottom: 0.25rem; }
        .summary-value { font-size: 1.25rem; font-weight: bold; }
        table { width: 100%; border-collapse: collapse; margin-bottom: 1rem; }
        th, td { text-align: left; padding: 0.75rem; border-bottom: 1px solid #2d3748; word-break: break-all; }
        th { background-color: #252d3d; color: #94a3b8; font-weight: normal; word-break: normal; }
        tr:nth-child(even) { background-color: #1a1f2e; }
        tr:nth-child(odd) { background-color: #151821; }
        .status-success { color: #68d391; }
        .status-partial { color: #f6ad55; }
        .status-failure { color: #fc8181; }
        details { background-color: #1e2130; border: 1px solid #2d3748; border-radius: 8px; margin-bottom: 1rem; overflow: hidden; }
        summary { padding: 1rem; cursor: pointer; background-color: #252d3d; font-weight: bold; outline: none; }
        summary:hover { background-color: #2d3748; }
        .details-content { padding: 1rem; overflow-x: auto; }
        .match-row { background-color: rgba(79, 152, 163, 0.2) !important; }
        .error-list { color: #fc8181; margin-top: 1rem; list-style-type: square; margin-left: 1.5rem; padding-left: 0; }
        .footer { margin-top: 3rem; color: #94a3b8; font-size: 0.875rem; text-align: center; border-top: 1px solid #2d3748; padding-top: 1rem; }
        ");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");

        // Section 1 - Header
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine("            <h1>GhostTrace <span class=\"accent\">Forensic Report</span></h1>");
        sb.AppendLine("            <hr style=\"border-color: #2d3748; margin-bottom: 1rem;\">");
        sb.AppendLine($"            <div style=\"display: grid; grid-template-columns: 150px 1fr; gap: 0.5rem;\">");
        sb.AppendLine($"                <span style=\"color: #94a3b8;\">Host</span><span>{WebUtility.HtmlEncode(report.MachineName)}</span>");
        sb.AppendLine($"                <span style=\"color: #94a3b8;\">OS</span><span>{WebUtility.HtmlEncode(report.OsVersion)}</span>");
        sb.AppendLine($"                <span style=\"color: #94a3b8;\">Scan Start</span><span>{WebUtility.HtmlEncode(report.ScanStartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zz"))}</span>");
        sb.AppendLine($"                <span style=\"color: #94a3b8;\">Duration</span><span>{WebUtility.HtmlEncode(report.Duration.ToString(@"hh\:mm\:ss\.fff"))}</span>");
        if (report.FilterTerm != null)
        {
            sb.AppendLine($"                <span style=\"color: #94a3b8;\">Filter</span><span class=\"accent\">{WebUtility.HtmlEncode(report.FilterTerm)}</span>");
        }
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // Determine Worst Status for Header Card (Skipped never raises severity)
        ScanStatus worstStatus = report.WorstStatus;

        string statusClass = worstStatus switch
        {
            ScanStatus.Success => "status-success",
            ScanStatus.PartialSuccess => "status-partial",
            ScanStatus.Skipped => "status-partial",
            _ => "status-failure"
        };
        string statusIcon = worstStatus switch
        {
            ScanStatus.Success => "&#10003;",
            ScanStatus.PartialSuccess => "&#9888;",
            ScanStatus.Skipped => "&#8211;",
            _ => "&#10007;"
        };

        // Section 2 - Executive Summary
        sb.AppendLine("        <div class=\"grid card\">");
        sb.AppendLine("            <div class=\"summary-item\">");
        sb.AppendLine("                <span class=\"summary-label\">Total Findings</span>");
        sb.AppendLine($"                <span class=\"summary-value\">{report.TotalFindings}</span>");
        sb.AppendLine("            </div>");
        if (report.FilterTerm != null)
        {
            sb.AppendLine("            <div class=\"summary-item\">");
            sb.AppendLine("                <span class=\"summary-label\">Matches</span>");
            sb.AppendLine($"                <span class=\"summary-value accent\">{report.TotalMatches}</span>");
            sb.AppendLine("            </div>");
        }
        sb.AppendLine("            <div class=\"summary-item\">");
        sb.AppendLine("                <span class=\"summary-label\">Modules Executed</span>");
        sb.AppendLine($"                <span class=\"summary-value\">{report.ModuleResults.Count}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"summary-item\">");
        sb.AppendLine("                <span class=\"summary-label\">Overall Status</span>");
        sb.AppendLine($"                <span class=\"summary-value {statusClass}\">{statusIcon} {worstStatus.ToString().ToUpper()}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // Section 3 - Modules Table
        sb.AppendLine("        <div class=\"card\">");
        sb.AppendLine("            <h2>Modules Summary</h2>");
        sb.AppendLine("            <div style=\"overflow-x: auto;\">");
        sb.AppendLine("            <table>");
        sb.AppendLine("                <tr>");
        sb.AppendLine("                    <th>Module</th>");
        sb.AppendLine("                    <th>Status</th>");
        sb.AppendLine("                    <th>Findings</th>");
        if (report.FilterTerm != null) sb.AppendLine("                    <th>Matches</th>");
        sb.AppendLine("                    <th>Duration</th>");
        sb.AppendLine("                    <th>Errors</th>");
        sb.AppendLine("                </tr>");

        foreach (var m in report.ModuleResults)
        {
            string mStatusClass = m.Status switch
            {
                ScanStatus.Success => "status-success",
                ScanStatus.PartialSuccess => "status-partial",
                ScanStatus.Skipped => "status-partial",
                _ => "status-failure"
            };
            string mStatusIcon = m.Status switch
            {
                ScanStatus.Success => "&#10003;",
                ScanStatus.PartialSuccess => "&#9888;",
                ScanStatus.Skipped => "&#8211;",
                _ => "&#10007;"
            };

            sb.AppendLine("                <tr>");
            sb.AppendLine($"                    <td>{WebUtility.HtmlEncode(m.ModuleName)}</td>");
            sb.AppendLine($"                    <td class=\"{mStatusClass}\">{mStatusIcon} {m.Status}</td>");
            sb.AppendLine($"                    <td>{m.TotalFindings}</td>");
            if (report.FilterTerm != null) sb.AppendLine($"                    <td>{m.TotalMatches}</td>");
            sb.AppendLine($"                    <td>{WebUtility.HtmlEncode(m.ModuleDuration.ToString(@"s\.fff"))}s</td>");
            sb.AppendLine($"                    <td>{m.Errors.Count}</td>");
            sb.AppendLine("                </tr>");
        }
        sb.AppendLine("            </table>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // Section 4 - Highlighted Matches (if FilterTerm != null)
        if (report.FilterTerm != null && report.TotalMatches > 0)
        {
            sb.AppendLine("        <div class=\"card\" style=\"border-color: #4f98a3;\">");
            sb.AppendLine($"            <h2>Artifacts related to <span class=\"accent\">\"{WebUtility.HtmlEncode(report.FilterTerm)}\"</span></h2>");
            sb.AppendLine("            <div style=\"overflow-x: auto;\">");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Module</th>");
            sb.AppendLine("                    <th>Category</th>");
            sb.AppendLine("                    <th>Source</th>");
            sb.AppendLine("                    <th>Description</th>");
            sb.AppendLine("                    <th>Timestamp</th>");
            sb.AppendLine("                    <th>RawValue</th>");
            sb.AppendLine("                </tr>");

            foreach (var m in report.ModuleResults)
            {
                foreach (var f in m.MatchedFindings)
                {
                    sb.AppendLine("                <tr class=\"match-row\">");
                    sb.AppendLine($"                    <td>{WebUtility.HtmlEncode(m.ModuleName)}</td>");
                    sb.AppendLine($"                    <td>{WebUtility.HtmlEncode(f.Category)}</td>");
                    sb.AppendLine($"                    <td>{WebUtility.HtmlEncode(f.Source)}</td>");
                    sb.AppendLine($"                    <td>{WebUtility.HtmlEncode(f.Description)}</td>");
                    sb.AppendLine($"                    <td style=\"white-space: nowrap;\">{WebUtility.HtmlEncode(f.TimestampUtc?.ToString("yyyy-MM-dd HH:mm:ssZ") ?? "-")}</td>");
                    sb.AppendLine($"                    <td><pre style=\"margin:0; white-space: pre-wrap; font-family: inherit;\">{WebUtility.HtmlEncode(f.RawValue ?? "-")}</pre></td>");
                    sb.AppendLine("                </tr>");
                }
            }
            sb.AppendLine("            </table>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
        }

        // Section 5 - Findings per module
        sb.AppendLine("        <h2>Detailed Findings</h2>");
        foreach (var m in report.ModuleResults)
        {
            sb.AppendLine("        <details>");
            sb.AppendLine($"            <summary>{WebUtility.HtmlEncode(m.ModuleName)} ({m.TotalFindings} findings)</summary>");
            sb.AppendLine("            <div class=\"details-content\">");

            if (m.TotalFindings > 0 || m.TotalMatches > 0)
            {
                sb.AppendLine("                <table>");
                sb.AppendLine("                    <tr>");
                sb.AppendLine("                        <th>Category</th>");
                sb.AppendLine("                        <th>Source</th>");
                sb.AppendLine("                        <th>Description</th>");
                sb.AppendLine("                        <th>Timestamp</th>");
                sb.AppendLine("                        <th>RawValue</th>");
                sb.AppendLine("                    </tr>");

                if (report.FilterTerm != null)
                {
                    foreach (var f in m.MatchedFindings)
                    {
                        sb.AppendLine("                    <tr class=\"match-row\">");
                        sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(f.Category)}</td>");
                        sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(f.Source)}</td>");
                        sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(f.Description)}</td>");
                        sb.AppendLine($"                        <td style=\"white-space: nowrap;\">{WebUtility.HtmlEncode(f.TimestampUtc?.ToString("yyyy-MM-dd HH:mm:ssZ") ?? "-")}</td>");
                        sb.AppendLine($"                        <td><pre style=\"margin:0; white-space: pre-wrap; font-family: inherit;\">{WebUtility.HtmlEncode(f.RawValue ?? "-")}</pre></td>");
                        sb.AppendLine("                    </tr>");
                    }
                }

                foreach (var f in m.Findings)
                {
                    sb.AppendLine("                    <tr>");
                    sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(f.Category)}</td>");
                    sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(f.Source)}</td>");
                    sb.AppendLine($"                        <td>{WebUtility.HtmlEncode(f.Description)}</td>");
                    sb.AppendLine($"                        <td style=\"white-space: nowrap;\">{WebUtility.HtmlEncode(f.TimestampUtc?.ToString("yyyy-MM-dd HH:mm:ssZ") ?? "-")}</td>");
                    sb.AppendLine($"                        <td><pre style=\"margin:0; white-space: pre-wrap; font-family: inherit;\">{WebUtility.HtmlEncode(f.RawValue ?? "-")}</pre></td>");
                    sb.AppendLine("                    </tr>");
                }
                sb.AppendLine("                </table>");
            }
            else
            {
                sb.AppendLine("                <p style=\"color: #94a3b8;\">No findings collected.</p>");
            }

            if (m.Errors.Count > 0)
            {
                sb.AppendLine("                <h3 style=\"color: #fc8181; margin-top: 1rem;\">Errors Encountered</h3>");
                sb.AppendLine("                <ul class=\"error-list\">");
                foreach (var err in m.Errors)
                {
                    sb.AppendLine($"                    <li>{WebUtility.HtmlEncode(err)}</li>");
                }
                sb.AppendLine("                </ul>");
            }

            sb.AppendLine("            </div>");
            sb.AppendLine("        </details>");
        }

        // Footer
        var appVersion = (System.Reflection.Assembly.GetEntryAssembly()
                          ?? System.Reflection.Assembly.GetExecutingAssembly())
                          .GetName().Version?.ToString(3) ?? "1.0.0";
        sb.AppendLine("        <div class=\"footer\">");
        sb.AppendLine($"            <p>Generated by <strong>GhostTrace v{appVersion}</strong></p>");
        sb.AppendLine("            <p>This report was generated by a read-only forensic scanner.<br>No files were modified, deleted, or transmitted during this scan.</p>");
        sb.AppendLine("        </div>");

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }
}
