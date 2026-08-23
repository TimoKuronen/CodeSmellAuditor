using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class AuditEngineTests
{
    [Fact]
    public async Task AuditFileAsync_WritesReportAndReturnsPassFailResult()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            string targetsPath = Path.Combine(workspace, "Targets");
            Directory.CreateDirectory(rulesPath);
            Directory.CreateDirectory(targetsPath);

            string filePath = Path.Combine(targetsPath, "Sample.cs");
            await File.WriteAllTextAsync(filePath, "public class Sample { }");

            var engine = new AuditEngine(
                new FakeRuleRepository(new AuditRule("rule.mdc", "Use interfaces.")),
                new FakeAiOrchestrator("Status: REVIEW REQUIRED"));

            IReadOnlyList<AuditRule> rules = await engine.LoadRulesAsync(rulesPath);
            string reportsPath = AuditEngine.ResolveReportsDirectory(rulesPath);

            FileAuditResult result = await engine.AuditFileAsync(filePath, rules, reportsPath);

            Assert.Equal("Sample.cs", result.FileName);
            Assert.False(result.HasPassed);
            Assert.True(File.Exists(result.ReportPath));

            string savedReport = await File.ReadAllTextAsync(result.ReportPath);
            Assert.Contains("TargetFile: Sample.cs", savedReport);
            Assert.Contains("Status: REVIEW REQUIRED", savedReport);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task RunBatchAsync_ReturnsEmptyResultWhenNoTargets()
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
                new FakeAiOrchestrator("Status: COMPLIANT"));

            AuditRunResult result = await engine.RunBatchAsync(rulesPath, targetsPath);

            Assert.Empty(result.FileResults);
            Assert.False(result.AllPassed);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task RunBatchAsync_AuditsAllTargets()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            string targetsPath = Path.Combine(workspace, "Targets");
            Directory.CreateDirectory(rulesPath);
            Directory.CreateDirectory(targetsPath);

            await File.WriteAllTextAsync(Path.Combine(targetsPath, "A.cs"), "class A {}");
            await File.WriteAllTextAsync(Path.Combine(targetsPath, "B.cs"), "class B {}");

            var engine = new AuditEngine(
                new FakeRuleRepository(new AuditRule("rule.mdc", "Use interfaces.")),
                new FakeAiOrchestrator("Status: COMPLIANT"));

            AuditRunResult result = await engine.RunBatchAsync(rulesPath, targetsPath);

            Assert.Equal(2, result.FileResults.Count);
            Assert.True(result.AllPassed);
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
