using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class MarkdownRuleRepositoryTests
{
    [Fact]
    public async Task GetActiveRulesAsync_LoadsMarkdownAndMdcFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"codesmell-rules-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "rule-one.md"),
                "Always use constructor injection.");

            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "rule-two.mdc"),
                "Avoid static mutable state.");

            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "empty.md"),
                "   ");

            var repository = new MarkdownRuleRepository();
            var rules = (await repository.GetActiveRulesAsync(tempDir)).ToList();

            Assert.Equal(2, rules.Count);
            Assert.Contains(rules, r => r.Name == "rule-one.md");
            Assert.Contains(rules, r => r.Name == "rule-two.mdc");
            Assert.Contains(rules, r => r.PromptGuideline.Contains("constructor injection"));
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
}

public class AuditReportTests
{
    [Fact]
    public void AuditReport_StoresPassStatus()
    {
        var report = new AuditReport("Test.cs", "All checks passed.", true);

        Assert.Equal("Test.cs", report.FilePath);
        Assert.True(report.HasPassed);
    }
}