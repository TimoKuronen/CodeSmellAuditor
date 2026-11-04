using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class FindingGroundingValidatorTests
{
    private static readonly SourceFile Core = new(
        @"C:\proj\Core.cs",
        "namespace Proj;\n\npublic class Core\n{\n    public void Run(int  count)\n    {\n    }\n\n    public void Run(int count) { }\n}\n");

    private static readonly SourceFile Ui = new("/proj/Ui.cs", "public class Ui\n{\n    private readonly Core _core = new Core();\n}\n");

    [Fact]
    public void Validate_EvidenceInFile_IsGroundedWithVerifiedLine()
    {
        AuditFinding result = ValidateSingle(Finding("private readonly Core _core = new Core();", file: "Ui.cs"));

        Assert.True(result.IsGrounded);
        Assert.Equal(3, result.VerifiedLine);
        Assert.Null(result.GroundingNote);
    }

    [Fact]
    public void Validate_WhitespaceDifferences_StillMatch()
    {
        AuditFinding result = ValidateSingle(Finding("public   void Run(int count)", file: "Core.cs"));

        Assert.True(result.IsGrounded);
    }

    [Fact]
    public void Validate_SeveralOccurrences_PrefersNearestReportedLine()
    {
        AuditFinding result = ValidateSingle(Finding("public void Run(int count)", file: "Core.cs", line: 8));

        Assert.Equal(9, result.VerifiedLine);
        Assert.Equal("reported line 8, found at 9", result.GroundingNote);
    }

    [Fact]
    public void Validate_NoFileGiven_SearchesAllSourcesAndFillsFile()
    {
        AuditFinding result = ValidateSingle(Finding("private readonly Core _core = new Core();"));

        Assert.True(result.IsGrounded);
        Assert.Equal("Ui.cs", result.File);
    }

    [Fact]
    public void Validate_EllipsisAbbreviation_MatchesSegmentsInOrder()
    {
        AuditFinding result = ValidateSingle(Finding("private readonly Core _core = new Core(...);", file: "Ui.cs"));

        Assert.True(result.IsGrounded);
        Assert.Equal(3, result.VerifiedLine);
    }

    [Theory]
    [InlineData("count) ... public void Run(int", "evidence not found in Core.cs")]
    [InlineData("public class Core ... namespace Proj;", "evidence not found in Core.cs")]
    [InlineData("Run(...)", "evidence too short to verify")]
    public void Validate_EllipsisSegments_RequireOrderAndLength(string evidence, string expectedNote)
    {
        AuditFinding result = ValidateSingle(Finding(evidence, file: "Core.cs"));

        Assert.False(result.IsGrounded);
        Assert.Equal(expectedNote, result.GroundingNote);
    }

    [Theory]
    [InlineData("", "no evidence quote")]
    [InlineData("}", "evidence too short to verify")]
    [InlineData("public void Stop(int count)", "evidence not found in Core.cs")]
    public void Validate_BadEvidence_IsUngrounded(string evidence, string expectedNote)
    {
        AuditFinding result = ValidateSingle(Finding(evidence, file: "Core.cs"));

        Assert.False(result.IsGrounded);
        Assert.Null(result.VerifiedLine);
        Assert.Equal(expectedNote, result.GroundingNote);
    }

    [Fact]
    public void Validate_FileOutsideAuditedSet_IsUngrounded()
    {
        AuditFinding result = ValidateSingle(Finding("public void Run(int count)", file: "Other.cs"));

        Assert.False(result.IsGrounded);
        Assert.Equal("file not in audited set: Other.cs", result.GroundingNote);
    }

    [Fact]
    public void Validate_EvidenceInWrongFile_IsUngrounded()
    {
        AuditFinding result = ValidateSingle(Finding("public void Run(int count)", file: "Ui.cs"));

        Assert.False(result.IsGrounded);
    }

    private static AuditFinding ValidateSingle(AuditFinding finding) =>
        Assert.Single(FindingGroundingValidator.Validate(new[] { finding }, new[] { Core, Ui }));

    private static AuditFinding Finding(string evidence, string? file = null, int? line = null) =>
        new("F1", file, line, Rule: null, Fix: null, evidence, "issue");
}
