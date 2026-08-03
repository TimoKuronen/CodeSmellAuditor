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
    public void ParsePassStatus_ReadsStatusLine(string body, bool expectedPass)
    {
        Assert.Equal(expectedPass, AuditReportParser.ParsePassStatus(body));
    }

    [Fact]
    public void ParsePassStatus_FallsBackWhenNoStatusLine()
    {
        Assert.False(AuditReportParser.ParsePassStatus("Some text with REVIEW REQUIRED in it"));
        Assert.True(AuditReportParser.ParsePassStatus("All checks passed."));
    }
}
