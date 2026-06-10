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
public class TxtReportWriterTests
{
    [Fact]
    public void Write_GeneratesTxtFile_WithExpectedSummary()
    {
        string tempFile = Path.GetTempFileName() + ".txt";
        try
        {
            var report = new FullScanReport
            {
                ScanStartedAt = DateTimeOffset.UtcNow,
                ScanFinishedAt = DateTimeOffset.UtcNow.AddSeconds(1),
                MachineName = "TEST-PC",
                OsVersion = "Windows Test",
                FilterTerm = null,
                TotalFindings = 10,
                TotalMatches = 0,
                ModuleResults = new List<ModuleScanResult>().AsReadOnly()
            };

            TxtReportWriter.Write(report, tempFile);

            string content = File.ReadAllText(tempFile);
            Assert.Contains("GhostTrace Forensic Report", content);
            Assert.Contains("TEST-PC", content);
            Assert.Contains("Windows Test", content);
            Assert.Contains("Duration", content);
            Assert.Contains("Status", content);
            Assert.DoesNotContain("Filter     :", content);
            Assert.DoesNotContain("Matched findings", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Write_WithFilterTerm_IncludesMatchesSection()
    {
        string tempFile = Path.GetTempFileName() + ".txt";
        try
        {
            var finding = new ScanFinding("File", "Test File", "C:\\Test", DateTimeOffset.UtcNow);
            var modResult = new ModuleScanResult
            {
                ModuleName = "TestMod",
                Status = ScanStatus.Success,
                Findings = new List<ScanFinding>().AsReadOnly(),
                MatchedFindings = new List<ScanFinding> { finding }.AsReadOnly(),
                Errors = new List<string>().AsReadOnly()
            };

            var report = new FullScanReport
            {
                FilterTerm = "Test",
                MachineName = "PC",
                OsVersion = "OS",
                ModuleResults = new List<ModuleScanResult> { modResult }.AsReadOnly()
            };

            TxtReportWriter.Write(report, tempFile);

            string content = File.ReadAllText(tempFile);
            Assert.Contains("Filter     : Test", content);
            Assert.Contains("Matched findings", content);
            Assert.Contains("[TestMod]", content);
            Assert.Contains("C:\\Test", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
