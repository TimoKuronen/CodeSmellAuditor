using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class AuditGroundingTests
{
    private const string Heading = "## Top Findings";
    private static readonly SourceFile[] Sources = { new("Sample.cs", "public class Sample\n{\n    public int Count { get; set; }\n}\n") };

    private const string GroundedFinding = "- [F1] Line: 3\n  Evidence: `public int Count { get; set; }`";
    private const string UngroundedFinding = "- [F1] Line: 3\n  Evidence: `private static int counter = 0;`";

    [Theory]
    [InlineData("REVIEW REQUIRED", GroundedFinding, AuditOutcome.ReviewRequired)]
    [InlineData("COMPLIANT", GroundedFinding, AuditOutcome.ReviewRequired)]
    [InlineData("REVIEW REQUIRED", UngroundedFinding, AuditOutcome.Passed)]
    [InlineData("REVIEW REQUIRED", "- None", AuditOutcome.Passed)]
    [InlineData("COMPLIANT", "- None", AuditOutcome.Passed)]
    public void Evaluate_WithFindingsSection_UsesGroundedFindingsOnly(
        string modelStatus,
        string findingsBody,
        AuditOutcome expected)
    {
        string report = $"Status: {modelStatus}\nScore: 70\n\n{Heading}\n{findingsBody}\n";

        AuditGrounding grounding = AuditGrounding.Evaluate(report, Heading, Sources);

        Assert.Equal(modelStatus, grounding.ModelStatus);
        Assert.True(grounding.FindingsSectionFound);
        Assert.Equal(expected, grounding.Outcome);
    }

    [Theory]
    [InlineData("Status: COMPLIANT", AuditOutcome.Passed)]
    [InlineData("Status: REVIEW REQUIRED", AuditOutcome.ReviewRequired)]
    public void Evaluate_WithoutFindingsSection_FollowsModelStatus(string statusLine, AuditOutcome expected)
    {
        AuditGrounding grounding = AuditGrounding.Evaluate($"{statusLine}\nScore: 80", Heading, Sources);

        Assert.False(grounding.FindingsSectionFound);
        Assert.Equal(expected, grounding.Outcome);
    }

    [Fact]
    public void Evaluate_MissingStatus_FailsClosedEvenWithoutGroundedFindings()
    {
        AuditGrounding grounding = AuditGrounding.Evaluate($"{Heading}\n- None", Heading, Sources);

        Assert.Null(grounding.ModelStatus);
        Assert.Equal(AuditOutcome.ReviewRequired, grounding.Outcome);
    }

    [Fact]
    public void Evaluate_CountsGroundedAndUngrounded()
    {
        string report = $"Status: REVIEW REQUIRED\n{Heading}\n{GroundedFinding}\n- [F2] Line: 9\n  Evidence: `missing line here;`";

        AuditGrounding grounding = AuditGrounding.Evaluate(report, Heading, Sources);

        Assert.Equal(1, grounding.GroundedCount);
        Assert.Equal(1, grounding.UngroundedCount);
    }
}
