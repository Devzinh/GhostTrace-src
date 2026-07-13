using GhostTrace.Core.Enums;
using GhostTrace.Core.Models;
using GhostTrace.Core.Reporting;
using GhostTrace.Core.Reports;

namespace GhostTrace.Tests;

public sealed class CsvReportWriterTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1+1")]
    [InlineData("-1+1")]
    [InlineData("@SUM(A1:A2)")]
    public void Write_PrefixesSpreadsheetFormulas(string untrustedValue)
    {
        string tempFile = Path.GetTempFileName() + ".csv";
        try
        {
            var finding = new ScanFinding("File", untrustedValue, "source", null, null);
            var report = new FullScanReport
            {
                ModuleResults =
                [
                    new ModuleScanResult
                    {
                        ModuleName = "Mod",
                        Status = ScanStatus.Success,
                        Findings = [finding],
                        MatchedFindings = Array.Empty<ScanFinding>(),
                        Errors = Array.Empty<string>()
                    }
                ]
            };

            CsvReportWriter.Write(report, tempFile);

            Assert.Contains("'" + untrustedValue, File.ReadAllText(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}