using CodeSmellAuditor.Cli;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class CliFixArgsTests
{
    [Fact]
    public void Parse_FixWithFolderAndFile_ReturnsFixMode()
    {
        CliArgs result = CliArgs.Parse(new[] { "fix", @"D:\Repo\Scripts", @"D:\Repo\One.cs" });

        Assert.Equal(CliMode.Fix, result.Mode);
        Assert.Equal(new[] { @"D:\Repo\Scripts", @"D:\Repo\One.cs" }, result.FixPaths);
        Assert.False(result.Yes);
        Assert.False(result.DryRun);
    }

    [Fact]
    public void Parse_FixFlags_AreRead()
    {
        Assert.True(CliArgs.Parse(new[] { "fix", "Scripts", "--yes" }).Yes);
        Assert.True(CliArgs.Parse(new[] { "fix", "--dry-run", "Scripts" }).DryRun);
    }

    [Fact]
    public void Parse_FixWithoutPath_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => CliArgs.Parse(new[] { "fix" }));

        Assert.Contains("at least one", ex.Message);
    }

    [Fact]
    public void Parse_YesAndDryRunTogether_Throws()
    {
        Assert.Throws<ArgumentException>(() => CliArgs.Parse(new[] { "fix", "Scripts", "--yes", "--dry-run" }));
    }

    [Theory]
    [InlineData("--yes")]
    [InlineData("--dry-run")]
    public void Parse_FixFlagsOutsideFix_Throw(string flag)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => CliArgs.Parse(new[] { "sniff", "A.cs", flag }));

        Assert.Contains("only valid with fix", ex.Message);
    }

    [Fact]
    public void Parse_FixIsCaseInsensitive()
    {
        Assert.Equal(CliMode.Fix, CliArgs.Parse(new[] { "FIX", "Scripts" }).Mode);
    }
}
