using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Localization;
using GhostTrace.Core.Reports;

namespace GhostTrace.Core.Reporting;

[SupportedOSPlatform("windows")]
public static class TxtReportWriter
{
    public static void Write(FullScanReport report, string outputPath)
    {
        var s = Loc.Current;
        var sb = new StringBuilder();

        sb.AppendLine(s.RptTitle);
        sb.AppendLine("============================================================");
        sb.AppendLine($"{s.RptHost,-10} : {report.MachineName}");
        sb.AppendLine($"{s.RptOs,-10} : {report.OsVersion}");
        sb.AppendLine($"{s.RptStarted,-10} : {report.ScanStartedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zz}");
        sb.AppendLine($"{s.RptFinished,-10} : {report.ScanFinishedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zz}");
        sb.AppendLine($"{s.RptDuration,-10} : {report.Duration.ToString(@"hh\:mm\:ss\.fff")}");

        if (report.FilterTerm != null)
        {
            sb.AppendLine($"{s.RptFilter,-10} : {report.FilterTerm}");
        }

        sb.AppendLine($"{s.RptFindings,-10} : {report.TotalFindings}");
        sb.AppendLine($"{s.RptMatches,-10} : {(report.FilterTerm != null ? report.TotalMatches : 0)}");
        sb.AppendLine($"{s.RptStatus,-10} : {report.WorstStatus.ToString().ToUpper()}");
        sb.AppendLine();
        sb.AppendLine(s.RptModules);
        sb.AppendLine("------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,-20} {1,-15} {2,8} {3,8} {4,7}  {5}",
            s.RptColName, s.RptColStatus, s.RptColFindings, s.RptColMatches, s.RptColErrors, s.RptColDuration));

        foreach (var m in report.ModuleResults)
        {
            sb.AppendLine(string.Format("{0,-20} {1,-15} {2,8} {3,8} {4,7}  {5}",
                m.ModuleName,
                m.Status.ToString(),
                m.TotalFindings,
                m.TotalMatches,
                m.Errors.Count,
                m.ModuleDuration.ToString(@"mm\:ss\.fff")));
        }

        if (report.FilterTerm != null)
        {
            sb.AppendLine();
            sb.AppendLine(s.RptMatchedFindings);
            sb.AppendLine("------------------------------------------------------------");

            foreach (var m in report.ModuleResults)
            {
                foreach (var f in m.MatchedFindings)
                {
                    sb.AppendLine($"[{m.ModuleName}] {f.TimestampUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "-"}");
                    sb.AppendLine($"  {s.RptDescription,-11}: {f.Description}");
                    sb.AppendLine($"  {s.RptSource,-11}: {f.Source}");
                    if (f.RawValue != null)
                    {
                        sb.AppendLine($"  {s.RptRawValue,-11}: {f.RawValue}");
                    }
                    sb.AppendLine();
                }
            }
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }
}
