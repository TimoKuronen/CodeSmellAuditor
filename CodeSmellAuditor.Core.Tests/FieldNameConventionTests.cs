using CodeSmellAuditor.Fix;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class FieldNameConventionTests
{
    [Theory]
    [InlineData("Health", "health")]
    [InlineData("m_Health", "health")]
    [InlineData("s_Instance", "instance")]
    [InlineData("k_MaxCount", "maxCount")]
    [InlineData("Retry_Count", "retryCount")]
    [InlineData("HPBar", "hpBar")]
    [InlineData("HP", "hp")]
    [InlineData("URL2", "url2")]
    [InlineData("X", "x")]
    public void ToCamelCase_ConvertsDisallowedNames(string name, string expected)
    {
        Assert.Equal(expected, FieldNameConvention.ToCamelCase(name));
    }

    [Theory]
    [InlineData("Health", true)]
    [InlineData("m_Health", true)]
    [InlineData("s_x", true)]
    [InlineData("health", false)]
    [InlineData("_health", false)]
    [InlineData("mHealth", false)]
    public void IsDisallowed_MatchesThePreCheckRule(string name, bool expected)
    {
        Assert.Equal(expected, FieldNameConvention.IsDisallowed(name));
    }

    [Fact]
    public void ToCamelCase_LeavesANameWithNoCoreUnchanged()
    {
        Assert.Equal("m_", FieldNameConvention.ToCamelCase("m_"));
    }

    [Theory]
    [InlineData("private int Retry_Count;", "Retry_Count")]
    [InlineData("private int m_Health;", "m_Health")]
    [InlineData("private int Health;", "Health")]
    public void ToCamelCase_ResultIsNotFlaggedByThePreCheck(string declaration, string name)
    {
        string source = $$"""
            public class Sample
            {
                {{declaration}}
            }
            """;

        string renamed = FieldNameConvention.ToCamelCase(name);
        Assert.Contains(SourcePreChecks.Run(new SourceFile("Sample.cs", source)), f => f.Check == SourcePreChecks.PrivateFieldNaming);
        Assert.DoesNotContain(
            SourcePreChecks.Run(new SourceFile("Sample.cs", source.Replace(name, renamed))),
            f => f.Check == SourcePreChecks.PrivateFieldNaming);
    }
}
