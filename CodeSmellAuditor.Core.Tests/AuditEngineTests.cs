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

            string reportFileName = Path.GetFileName(result.ReportPath);
            Assert.StartsWith("Sample_", reportFileName);
            Assert.EndsWith("_Critique.md", reportFileName);

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

    [Fact]
    public async Task AuditSystemAsync_WritesCombinedReport()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            string targetsPath = Path.Combine(workspace, "Targets");
            Directory.CreateDirectory(rulesPath);
            Directory.CreateDirectory(targetsPath);

            string aPath = Path.Combine(targetsPath, "A.cs");
            string bPath = Path.Combine(targetsPath, "B.cs");
            await File.WriteAllTextAsync(aPath, "class A {}");
            await File.WriteAllTextAsync(bPath, "class B {}");

            var orchestrator = new FakeAiOrchestrator("Status: COMPLIANT\nScore: 90");
            var engine = new AuditEngine(
                new FakeRuleRepository(new AuditRule("arch.mdc", "Respect load order.")),
                orchestrator);

            string reportsPath = AuditEngine.ResolveReportsDirectory(rulesPath);
            FileAuditResult result = await engine.AuditSystemAsync(
                new[] { aPath, bPath },
                "Core before UI",
                await engine.LoadRulesAsync(rulesPath),
                reportsPath);

            Assert.Equal(SystemAuditIdentity.ReportBaseName, result.FileName);
            Assert.True(result.HasPassed);
            Assert.True(File.Exists(result.ReportPath));
            Assert.Equal(2, orchestrator.LastSystemFileCount);
            Assert.Equal("Core before UI", orchestrator.LastManifestText);

            string savedReport = await File.ReadAllTextAsync(result.ReportPath);
            Assert.Contains("TargetFile: A.cs, B.cs", savedReport);
            Assert.Contains("Status: COMPLIANT", savedReport);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task AuditSystemAsync_ThrowsWhenFewerThanTwoFiles()
    {
        var engine = new AuditEngine(
            new FakeRuleRepository(new AuditRule("arch.mdc", "Respect load order.")),
            new FakeAiOrchestrator("Status: COMPLIANT"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.AuditSystemAsync(
                new[] { "Only.cs" },
                null,
                Array.Empty<AuditRule>(),
                Path.GetTempPath()));
    }

    [Fact]
    public void ResolveArchitectureRulesPath_AppendsArchitectureFolder()
    {
        string resolved = AuditEngine.ResolveArchitectureRulesPath(@"D:\Storage\Rules");

        Assert.Equal(Path.Combine(@"D:\Storage\Rules", "Architecture"), resolved);
    }

    [Fact]
    public void ResolveStackRulesPath_AppendsStacksFolderAndName()
    {
        string resolved = AuditEngine.ResolveStackRulesPath(@"D:\Storage\Rules", "Unity");

        Assert.Equal(Path.Combine(@"D:\Storage\Rules", "Stacks", "Unity"), resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveStackRulesPath_RejectsEmptyName(string stackName)
    {
        Assert.Throws<ArgumentException>(() =>
            AuditEngine.ResolveStackRulesPath(@"D:\Storage\Rules", stackName));
    }

    [Theory]
    [InlineData("../Unity")]
    [InlineData("Unity/Extra")]
    [InlineData(@"Unity\Extra")]
    public void ResolveStackRulesPath_RejectsPathTraversal(string stackName)
    {
        Assert.Throws<ArgumentException>(() =>
            AuditEngine.ResolveStackRulesPath(@"D:\Storage\Rules", stackName));
    }

    [Fact]
    public async Task LoadRulesAsync_WithStack_ConcatenatesBaseAndStackRules()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            string stackPath = Path.Combine(rulesPath, "Stacks", "Unity");
            Directory.CreateDirectory(rulesPath);
            Directory.CreateDirectory(stackPath);

            await File.WriteAllTextAsync(
                Path.Combine(rulesPath, "base.mdc"),
                "Base rule body.");
            await File.WriteAllTextAsync(
                Path.Combine(stackPath, "unity.mdc"),
                "Unity rule body.");

            var engine = new AuditEngine(
                new MarkdownRuleRepository(),
                new FakeAiOrchestrator("Status: COMPLIANT"));

            IReadOnlyList<AuditRule> baseOnly = await engine.LoadRulesAsync(rulesPath);
            IReadOnlyList<AuditRule> withStack = await engine.LoadRulesAsync(rulesPath, "Unity");

            Assert.Single(baseOnly);
            Assert.Equal(2, withStack.Count);
            Assert.Contains(withStack, r => r.Name == "base.mdc");
            Assert.Contains(withStack, r => r.Name == "unity.mdc");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task LoadRulesAsync_WithStack_UsesStackRootWhenProvided()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            string architecturePath = Path.Combine(rulesPath, "Architecture");
            string stackPath = Path.Combine(rulesPath, "Stacks", "Unity");
            Directory.CreateDirectory(architecturePath);
            Directory.CreateDirectory(stackPath);

            await File.WriteAllTextAsync(
                Path.Combine(architecturePath, "arch.mdc"),
                "Architecture rule.");
            await File.WriteAllTextAsync(
                Path.Combine(stackPath, "unity.mdc"),
                "Unity rule.");

            var engine = new AuditEngine(
                new MarkdownRuleRepository(),
                new FakeAiOrchestrator("Status: COMPLIANT"));

            IReadOnlyList<AuditRule> rules = await engine.LoadRulesAsync(
                architecturePath,
                "Unity",
                stackRootRulesPath: rulesPath);

            Assert.Equal(2, rules.Count);
            Assert.Contains(rules, r => r.Name == "arch.mdc");
            Assert.Contains(rules, r => r.Name == "unity.mdc");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task LoadRulesAsync_WithMissingStack_Throws()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            Directory.CreateDirectory(rulesPath);

            var engine = new AuditEngine(
                new MarkdownRuleRepository(),
                new FakeAiOrchestrator("Status: COMPLIANT"));

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                engine.LoadRulesAsync(rulesPath, "Unity"));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSystemRulesAsync_LoadsArchitectureAndOptionalStack()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            string architecturePath = Path.Combine(rulesPath, "Architecture");
            string stackPath = Path.Combine(rulesPath, "Stacks", "Unity");
            Directory.CreateDirectory(architecturePath);
            Directory.CreateDirectory(stackPath);

            await File.WriteAllTextAsync(Path.Combine(architecturePath, "arch.mdc"), "Architecture rule.");
            await File.WriteAllTextAsync(Path.Combine(stackPath, "unity.mdc"), "Unity rule.");

            var engine = new AuditEngine(
                new MarkdownRuleRepository(),
                new FakeAiOrchestrator("Status: COMPLIANT"));

            IReadOnlyList<AuditRule> architectureOnly = await engine.LoadSystemRulesAsync(rulesPath);
            IReadOnlyList<AuditRule> withStack = await engine.LoadSystemRulesAsync(rulesPath, "Unity");

            Assert.Single(architectureOnly);
            Assert.Equal(2, withStack.Count);
            Assert.Contains(withStack, r => r.Name == "arch.mdc");
            Assert.Contains(withStack, r => r.Name == "unity.mdc");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSystemRulesAsync_ThrowsWhenArchitectureFolderMissing()
    {
        string workspace = CreateTempWorkspace();

        try
        {
            string rulesPath = Path.Combine(workspace, "Rules");
            Directory.CreateDirectory(rulesPath);

            var engine = new AuditEngine(
                new MarkdownRuleRepository(),
                new FakeAiOrchestrator("Status: COMPLIANT"));

            await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                engine.LoadSystemRulesAsync(rulesPath));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task AuditFileAsync_ToolFailure_SkipsReportPersist()
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
                new FailingAiOrchestrator());

            string reportsPath = AuditEngine.ResolveReportsDirectory(rulesPath);
            FileAuditResult result = await engine.AuditFileAsync(
                filePath,
                await engine.LoadRulesAsync(rulesPath),
                reportsPath);

            Assert.Equal(AuditOutcome.Failed, result.Outcome);
            Assert.Equal(string.Empty, result.ReportPath);
            Assert.Empty(Directory.GetFiles(reportsPath));
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

        public int LastSystemFileCount { get; private set; }

        public string? LastManifestText { get; private set; }

        public Task<AuditReport> AnalyzeCodeAsync(
            SourceFile file,
            IEnumerable<AuditRule> rules,
            Action<string> onTokenReceived)
        {
            onTokenReceived(_critique);
            return Task.FromResult(AuditReport.FromParsedStatus(file.FilePath, _critique));
        }

        public Task<AuditReport> AnalyzeSystemAsync(
            IReadOnlyList<SourceFile> files,
            string? manifestText,
            IEnumerable<AuditRule> rules,
            Action<string> onTokenReceived)
        {
            LastSystemFileCount = files.Count;
            LastManifestText = manifestText;
            onTokenReceived(_critique);
            return Task.FromResult(
                AuditReport.FromParsedStatus(SystemAuditIdentity.ReportBaseName, _critique));
        }
    }

    private sealed class FailingAiOrchestrator : IAiOrchestrator
    {
        public Task<AuditReport> AnalyzeCodeAsync(
            SourceFile file,
            IEnumerable<AuditRule> rules,
            Action<string> onTokenReceived)
        {
            return Task.FromResult(
                AuditReport.ToolFailure(file.FilePath, "# Local Engine Failure\n\nUnavailable"));
        }

        public Task<AuditReport> AnalyzeSystemAsync(
            IReadOnlyList<SourceFile> files,
            string? manifestText,
            IEnumerable<AuditRule> rules,
            Action<string> onTokenReceived)
        {
            return Task.FromResult(
                AuditReport.ToolFailure(SystemAuditIdentity.ReportBaseName, "# Local Engine Failure\n\nUnavailable"));
        }
    }
}
