using System;
using System.Runtime.Versioning;
using System.Collections.Generic;
using System.IO;
using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;
using GhostTrace.Core.Reports;
using GhostTrace.Core.Reporting;
using Xunit;

namespace GhostTrace.Tests.Unit.Core.Reporting;

[SupportedOSPlatform("windows")]
public class HtmlReportWriterTests
{
    [Fact]
    public void Write_GeneratesHtmlFile_WithEscapedData_AndExpectedStructure()
    {
        string tempFile = Path.GetTempFileName() + ".html";
        try
        {
            var finding = new ScanFinding("File", "Desc <script>", "Path & Source", DateTimeOffset.UtcNow, "Val \"1\"");
            var modResult = new ModuleScanResult
            {
                ModuleName = "FakeModule",
                Status = ScanStatus.Success,
                TotalFindings = 1,
                TotalMatches = 1,
                ModuleDuration = TimeSpan.FromSeconds(1),
                Findings = new List<ScanFinding>().AsReadOnly(),
                MatchedFindings = new List<ScanFinding> { finding }.AsReadOnly(),
                Errors = new List<string>().AsReadOnly()
            };

            var report = new FullScanReport
            {
                ScanStartedAt = DateTimeOffset.UtcNow,
                ScanFinishedAt = DateTimeOffset.UtcNow,
                MachineName = "TEST-PC",
                OsVersion = "Windows Test",
                FilterTerm = "Desc",
                TotalFindings = 1,
                TotalMatches = 1,
                ModuleResults = new List<ModuleScanResult> { modResult }.AsReadOnly()
            };

            HtmlReportWriter.Write(report, tempFile);

            string content = File.ReadAllText(tempFile);
            Assert.Contains("GhostTrace Report", content);
            Assert.Contains("&lt;script&gt;", content);
            Assert.Contains("Path &amp; Source", content);
            Assert.Contains("Val &quot;1&quot;", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Write_WithoutFilterTerm_DoesNotIncludeMatchesSection()
    {
        string tempFile = Path.GetTempFileName() + ".html";
        try
        {
            var report = new FullScanReport
            {
                FilterTerm = null,
                MachineName = "TEST-PC",
                OsVersion = "Windows Test",
                ModuleResults = new List<ModuleScanResult>().AsReadOnly()
            };

            HtmlReportWriter.Write(report, tempFile);

            string content = File.ReadAllText(tempFile);
            Assert.DoesNotContain("Matched findings", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
