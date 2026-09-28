using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class SystemAuditPromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_IncludesArchitectureFramingAndTemplate()
    {
        var rules = new List<AuditRule>
        {
            new("50-cross-file-dependencies.mdc", "Respect stated load order.")
        };

        string prompt = SystemAuditPromptBuilder.BuildSystemPrompt(rules);

        Assert.Contains("architecture auditor", prompt);
        Assert.Contains("[RULE: 50-cross-file-dependencies.mdc]", prompt);
        Assert.Contains("Respect stated load order.", prompt);
        Assert.Contains("## Manifest Conformance", prompt);
        Assert.Contains("## Cross-File Findings", prompt);
        Assert.Contains("REVIEW REQUIRED", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesManifestAndFiles()
    {
        var files = new List<SourceFile>
        {
            new(@"C:\proj\Core.cs", "class Core {}"),
            new(@"C:\proj\Ui.cs", "class Ui {}")
        };

        string prompt = SystemAuditPromptBuilder.BuildUserPrompt(files, "Core before Ui");

        Assert.Contains("File count: 2", prompt);
        Assert.Contains("Core before Ui", prompt);
        Assert.Contains("--- File: Core.cs ---", prompt);
        Assert.Contains("--- File: Ui.cs ---", prompt);
        Assert.Contains("class Core {}", prompt);
        Assert.Contains("class Ui {}", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithoutManifest_StatesNoneProvided()
    {
        var files = new List<SourceFile>
        {
            new("A.cs", "class A {}"),
            new("B.cs", "class B {}")
        };

        string prompt = SystemAuditPromptBuilder.BuildUserPrompt(files, null);

        Assert.Contains("none provided", prompt);
    }

    [Fact]
    public void BuildUserPrompt_ThrowsWhenFewerThanTwoFiles()
    {
        var files = new List<SourceFile> { new("A.cs", "class A {}") };

        Assert.Throws<ArgumentException>(() => SystemAuditPromptBuilder.BuildUserPrompt(files, null));
    }

    [Fact]
    public void ForSystemAudit_UsesLargerBudgets()
    {
        AuditConfiguration config = AuditConfiguration.ForSystemAudit("qwen3.5:4b");

        Assert.Equal("qwen3.5:4b", config.ModelName);
        Assert.Equal(16384, config.NumCtx);
        Assert.Equal(2000, config.NumPredict);
        Assert.Equal(48000, config.MaxInputCharacters);
    }
}
