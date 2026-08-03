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
