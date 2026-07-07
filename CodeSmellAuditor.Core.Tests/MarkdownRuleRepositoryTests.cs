using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class MarkdownRuleRepositoryTests
{
    [Fact]
    public async Task GetActiveRulesAsync_LoadsMdcFilesOnly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"codesmell-rules-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "rule-one.mdc"),
                "---\ndescription: test\n---\nAlways use constructor injection.");

            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "reference-manual.md"),
                "This long reference manual should not be loaded at runtime.");

            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "empty.mdc"),
                "   ");

            var repository = new MarkdownRuleRepository();
            var rules = (await repository.GetActiveRulesAsync(tempDir)).ToList();

            Assert.Single(rules);
            Assert.Equal("rule-one.mdc", rules[0].Name);
            Assert.Contains("constructor injection", rules[0].PromptGuideline);
            Assert.DoesNotContain("description:", rules[0].PromptGuideline);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetActiveRulesAsync_ThrowsWhenFolderMissing()
    {
        var repository = new MarkdownRuleRepository();
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => repository.GetActiveRulesAsync(missingPath));
    }

    [Theory]
    [InlineData("---\nkey: value\n---\nBody text", "Body text")]
    [InlineData("No front matter here", "No front matter here")]
    public void StripFrontMatter_RemovesYamlHeader(string input, string expected)
    {
        Assert.Equal(expected, MarkdownRuleRepository.StripFrontMatter(input));
    }
}

public class AuditPromptBuilderTests
{
    [Fact]
    public void BuildSystemPrompt_IncludesRulesAndOutputTemplate()
    {
        var rules = new List<AuditRule>
        {
            new("test.mdc", "Prefer interfaces at boundaries.")
        };

        string prompt = AuditPromptBuilder.BuildSystemPrompt(rules);

        Assert.Contains("[RULE: test.mdc]", prompt);
        Assert.Contains("Prefer interfaces at boundaries.", prompt);
        Assert.Contains("Status: COMPLIANT or REVIEW REQUIRED", prompt);
        Assert.Contains("No analysis traces", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesFileNameAndSource()
    {
        var file = new SourceFile("C:\\targets\\SampleService.cs", "public class SampleService { }");

        string prompt = AuditPromptBuilder.BuildUserPrompt(file);

        Assert.Contains("SampleService.cs", prompt);
        Assert.Contains("public class SampleService", prompt);
    }

    [Fact]
    public void ValidateBudget_ThrowsWhenInputTooLarge()
    {
        var config = new AuditConfiguration(MaxInputCharacters: 100);
        string system = new string('a', 60);
        string user = new string('b', 60);

        var ex = Assert.Throws<InvalidOperationException>(
            () => AuditPromptBuilder.ValidateBudget(system, user, config));

        Assert.Contains("character budget", ex.Message);
    }

    [Fact]
    public void ValidateBudget_PassesWhenWithinLimit()
    {
        var config = new AuditConfiguration(MaxInputCharacters: 1000);

        AuditPromptBuilder.ValidateBudget("system", "user", config);
    }
}

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

public class AuditReportTests
{
    [Fact]
    public void AuditReport_StoresPassStatus()
    {
        var report = new AuditReport("Test.cs", "Status: COMPLIANT", true);

        Assert.Equal("Test.cs", report.FilePath);
        Assert.True(report.HasPassed);
    }
}
