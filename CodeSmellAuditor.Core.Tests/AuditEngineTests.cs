using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class AuditEngineTests
{
    [Fact]
    public async Task RunAsync_WritesReportAndReturnsPassFailResult()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            string targetsPath = Path.Combine(workspace, "Targets");
            Directory.CreateDirectory(rulesPath);
            Directory.CreateDirectory(targetsPath);

            await File.WriteAllTextAsync(
                Path.Combine(targetsPath, "Sample.cs"),
                "public class Sample { }");

            var engine = new AuditEngine(
                new FakeRuleRepository(new AuditRule("rule.mdc", "Use interfaces.")),
                new FakeAiOrchestrator("Status: REVIEW REQUIRED"),
                new NullAuditProgressReporter());

            AuditRunResult result = await engine.RunAsync(rulesPath, targetsPath);

            Assert.Single(result.FileResults);
            Assert.Equal("Sample.cs", result.FileResults[0].FileName);
            Assert.False(result.FileResults[0].HasPassed);
            Assert.False(result.AllPassed);
            Assert.True(File.Exists(result.FileResults[0].ReportPath));

            string savedReport = await File.ReadAllTextAsync(result.FileResults[0].ReportPath);
            Assert.Contains("TargetFile: Sample.cs", savedReport);
            Assert.Contains("Status: REVIEW REQUIRED", savedReport);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ReturnsEmptyResultWhenNoTargets()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            string targetsPath = Path.Combine(workspace, "Targets");
            Directory.CreateDirectory(rulesPath);
            Directory.CreateDirectory(targetsPath);

            var engine = new AuditEngine(
                new FakeRuleRepository(new AuditRule("rule.mdc", "Use interfaces.")),
                new FakeAiOrchestrator("Status: COMPLIANT"),
                new NullAuditProgressReporter());

            AuditRunResult result = await engine.RunAsync(rulesPath, targetsPath);

            Assert.Empty(result.FileResults);
            Assert.False(result.AllPassed);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static string CreateTempWorkspace()
    {
        return Path.Combine(Path.GetTempPath(), $"codesmell-engine-{Guid.NewGuid():N}");
    }

    private sealed class FakeRuleRepository : IRuleRepository
    {
        private readonly IReadOnlyList<AuditRule> _rules;

        public FakeRuleRepository(params AuditRule[] rules)
        {
            _rules = rules;
        }

        public Task<IEnumerable<AuditRule>> GetActiveRulesAsync(string rulesFolderPath)
        {
            return Task.FromResult<IEnumerable<AuditRule>>(_rules);
        }
    }

    private sealed class FakeAiOrchestrator : IAiOrchestrator
    {
        private readonly string _critique;

        public FakeAiOrchestrator(string critique)
        {
            _critique = critique;
        }

        public Task<AuditReport> AnalyzeCodeAsync(
            SourceFile file,
            IEnumerable<AuditRule> rules,
            Action<string> onTokenReceived)
        {
            onTokenReceived(_critique);
            bool hasPassed = AuditReportParser.ParsePassStatus(_critique);
            return Task.FromResult(new AuditReport(file.FilePath, _critique, hasPassed));
        }
    }
}
