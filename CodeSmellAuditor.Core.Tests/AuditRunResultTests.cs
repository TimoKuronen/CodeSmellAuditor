using CodeSmellAuditor.Cli;
using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class AuditRunResultTests
{
    [Fact]
    public void ExitCode_AllPassed_IsZero()
    {
        var result = new AuditRunResult(new[]
        {
            new FileAuditResult("A.cs", "a.md", AuditOutcome.Passed),
            new FileAuditResult("B.cs", "b.md", AuditOutcome.Passed)
        });

        Assert.True(result.AllPassed);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void ExitCode_ReviewRequired_IsOne()
    {
        var result = new AuditRunResult(new[]
        {
            new FileAuditResult("A.cs", "a.md", AuditOutcome.Passed),
            new FileAuditResult("B.cs", "b.md", AuditOutcome.ReviewRequired)
        });

        Assert.False(result.AllPassed);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void ExitCode_ToolFailure_IsTwo()
    {
        var result = new AuditRunResult(new[]
        {
            new FileAuditResult("A.cs", "a.md", AuditOutcome.Passed),
            new FileAuditResult("B.cs", "b.md", AuditOutcome.Failed)
        });

        Assert.False(result.AllPassed);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void ExitCode_FailedDominatesReviewRequired()
    {
        var result = new AuditRunResult(new[]
        {
            new FileAuditResult("A.cs", "a.md", AuditOutcome.ReviewRequired),
            new FileAuditResult("B.cs", "b.md", AuditOutcome.Failed)
        });

        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public void ExitCode_EmptyRun_IsOne()
    {
        var result = new AuditRunResult(Array.Empty<FileAuditResult>());

        Assert.False(result.AllPassed);
        Assert.Equal(1, result.ExitCode);
    }
}

public class BatchRunSummaryTests
{
    [Fact]
    public void Format_EmptyRun_ReportsZeroFiles()
    {
        string text = BatchRunSummary.Format(new AuditRunResult(Array.Empty<FileAuditResult>()));

        Assert.Equal("Batch summary: 0 files audited.", text);
    }

    [Fact]
    public void Format_MixedResults_ListsPassFailAndError()
    {
        var result = new AuditRunResult(new[]
        {
            new FileAuditResult("Good.cs", "g.md", AuditOutcome.Passed),
            new FileAuditResult("Bad.cs", "b.md", AuditOutcome.ReviewRequired),
            new FileAuditResult("Down.cs", string.Empty, AuditOutcome.Failed)
        });

        string text = BatchRunSummary.Format(result);

        Assert.Contains("Batch summary: 3 files - 1 passed, 1 review required, 1 failed", text);
        Assert.Contains("PASS  Good.cs", text);
        Assert.Contains("FAIL  Bad.cs", text);
        Assert.Contains("ERROR  Down.cs", text);
    }
}
