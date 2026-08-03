using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

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
