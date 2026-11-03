using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class AuditFindingParserTests
{
    private const string Heading = "## Top Findings";

    [Fact]
    public void Parse_TemplateShape_ReadsAllFields()
    {
        string report = """
            Status: REVIEW REQUIRED

            ## Top Findings
            - [F1] File: Sample.cs | Line: 12 | Rule: 10-csharp-style.mdc | Fix: mechanical
              Evidence: `public void DoWork(int count)`
              Issue: DoWork has no guard on count.

            ## Missing Context
            - Caller not visible
            """;

        AuditFinding finding = Assert.Single(AuditFindingParser.Parse(report, Heading)!);

        Assert.Equal("F1", finding.Id);
        Assert.Equal("Sample.cs", finding.File);
        Assert.Equal(12, finding.ReportedLine);
        Assert.Equal("10-csharp-style.mdc", finding.Rule);
        Assert.Equal("mechanical", finding.Fix);
        Assert.Equal("public void DoWork(int count)", finding.Evidence);
        Assert.Equal("DoWork has no guard on count.", finding.Issue);
    }

    [Fact]
    public void Parse_SubBulletFieldsAndQuotedEvidence_StayInOneFinding()
    {
        string report = """
            ## Top Findings
            - [F1] Line: 3 | Fix: judgment
              - Evidence: "var cache = new Dictionary<string, int>();"
              - Issue: Cache is never cleared.
            - [F2] Line: 9
              Evidence: 'return cache[key];'
            """;

        IReadOnlyList<AuditFinding> findings = AuditFindingParser.Parse(report, Heading)!;

        Assert.Equal(2, findings.Count);
        Assert.Equal("var cache = new Dictionary<string, int>();", findings[0].Evidence);
        Assert.Equal("Cache is never cleared.", findings[0].Issue);
        Assert.Equal("return cache[key];", findings[1].Evidence);
    }

    [Theory]
    [InlineData("- None")]
    [InlineData("- None detected; all files follow the rules.")]
    [InlineData("- N/A")]
    public void Parse_NoneStatement_ReturnsEmptyList(string bullet)
    {
        string report = $"## Top Findings\n{bullet}\n\n## Missing Context\n- x";

        IReadOnlyList<AuditFinding>? findings = AuditFindingParser.Parse(report, Heading);

        Assert.NotNull(findings);
        Assert.Empty(findings);
    }

    [Fact]
    public void Parse_MissingSection_ReturnsNull()
    {
        Assert.Null(AuditFindingParser.Parse("Status: COMPLIANT\nScore: 90", Heading));
    }

    [Fact]
    public void Parse_LegacyBullet_HasEmptyEvidenceAndAssignedId()
    {
        string report = "## Top Findings\n- Constructor takes 8 dependencies\n- Missing null checks";

        IReadOnlyList<AuditFinding> findings = AuditFindingParser.Parse(report, Heading)!;

        Assert.Equal(new[] { "F1", "F2" }, findings.Select(f => f.Id));
        Assert.All(findings, f => Assert.Equal(string.Empty, f.Evidence));
        Assert.Equal("Constructor takes 8 dependencies", findings[0].Issue);
    }

    [Fact]
    public void Parse_UnknownFixValue_IsDropped()
    {
        string report = "## Top Findings\n- [F1] Fix: easy\n  Evidence: `int x = 0;`";

        Assert.Null(Assert.Single(AuditFindingParser.Parse(report, Heading)!).Fix);
    }
}
