using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class AuditReportParserTests
{
    [Theory]
    [InlineData("Status: COMPLIANT", true)]
    [InlineData("Status: REVIEW REQUIRED", false)]
    [InlineData("# Report\nStatus: COMPLIANT\nScore: 90", true)]
    [InlineData("# Report\nStatus: REVIEW REQUIRED\nScore: 40", false)]
    [InlineData("status: compliant", true)]
    [InlineData("Status: Review Required", false)]
    public void ParsePassStatus_ReadsExactStatusLine(string body, bool expectedPass)
    {
        Assert.Equal(expectedPass, AuditReportParser.ParsePassStatus(body));
    }

    [Fact]
    public void ParsePassStatus_MissingStatusLine_FailsClosed()
    {
        Assert.False(AuditReportParser.ParsePassStatus("Some text with REVIEW REQUIRED in it"));
        Assert.False(AuditReportParser.ParsePassStatus("All checks passed."));
        Assert.False(AuditReportParser.ParsePassStatus("This report looks COMPLIANT overall."));
    }

    [Fact]
    public void ParsePassStatus_EmptyOrWhitespace_FailsClosed()
    {
        Assert.False(AuditReportParser.ParsePassStatus(""));
        Assert.False(AuditReportParser.ParsePassStatus("   \n\t  "));
        Assert.False(AuditReportParser.ParsePassStatus(null!));
    }

    [Fact]
    public void ParsePassStatus_UnknownStatusValue_FailsClosed()
    {
        Assert.False(AuditReportParser.ParsePassStatus("Status: PASS"));
        Assert.False(AuditReportParser.ParsePassStatus("Status: COMPLIANT WITH NOTES"));
        Assert.False(AuditReportParser.ParsePassStatus("Status:"));
    }

    [Fact]
    public void ParsePassStatus_TruncatedReportWithoutStatus_FailsClosed()
    {
        string truncated = """
            # Audit Report: SampleService.cs

            ## Top Findings
            - Domain logic references UI directly
            """;

        Assert.False(AuditReportParser.ParsePassStatus(truncated));
    }
}
