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
public class CsvReportWriterTests
{
    [Fact]
    public void Write_GeneratesCsvFile_WithHeader_AndEscapesProperly()
    {
        string tempFile = Path.GetTempFileName() + ".csv";
        try
        {
            var finding = new ScanFinding("File", "Desc with, comma", "Source with \"quotes\"", null, "Val\nNewline");
            var modResult = new ModuleScanResult
            {
                ModuleName = "Mod",
                Status = ScanStatus.Success,
                Findings = new List<ScanFinding>().AsReadOnly(),
                MatchedFindings = new List<ScanFinding> { finding }.AsReadOnly(),
                Errors = new List<string>().AsReadOnly()
            };

            var report = new FullScanReport
            {
                MachineName = "PC",
                OsVersion = "OS",
                ModuleResults = new List<ModuleScanResult> { modResult }.AsReadOnly()
            };

            CsvReportWriter.Write(report, tempFile);

            var lines = File.ReadAllLines(tempFile);
            // Because of \n in "Val\nNewline", ReadAllLines will split it.
            // Let's just read all text and verify presence.
            string content = File.ReadAllText(tempFile);
            Assert.Contains("Module,Category,Description,Source,TimestampUtc,RawValue,IsMatch,Status", content);
            
            // Check escaping
            Assert.Contains("\"Desc with, comma\"", content);
            Assert.Contains("\"Source with \"\"quotes\"\"\"", content);
            Assert.Contains("\"Val\nNewline\"", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
