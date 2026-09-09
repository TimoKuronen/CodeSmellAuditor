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
            new FileAuditResult("A.cs", "a.md", HasPassed: true),
            new FileAuditResult("B.cs", "b.md", HasPassed: true)
        });

        Assert.True(result.AllPassed);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void ExitCode_AnyFailure_IsOne()
    {
        var result = new AuditRunResult(new[]
        {
            new FileAuditResult("A.cs", "a.md", HasPassed: true),
            new FileAuditResult("B.cs", "b.md", HasPassed: false)
        });

        Assert.False(result.AllPassed);
        Assert.Equal(1, result.ExitCode);
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
    public void Format_MixedResults_ListsPassFailPerFile()
    {
        var result = new AuditRunResult(new[]
        {
            new FileAuditResult("Good.cs", "g.md", HasPassed: true),
            new FileAuditResult("Bad.cs", "b.md", HasPassed: false)
        });

        string text = BatchRunSummary.Format(result);

        Assert.Contains("Batch summary: 2 files - 1 passed, 1 failed", text);
        Assert.Contains("PASS  Good.cs", text);
        Assert.Contains("FAIL  Bad.cs", text);
    }
}
