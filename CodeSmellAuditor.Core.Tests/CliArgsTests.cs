using CodeSmellAuditor.Cli;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class CliArgsTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsBatchMode()
    {
        CliArgs result = CliArgs.Parse(Array.Empty<string>());

        Assert.Equal(CliMode.Batch, result.Mode);
        Assert.Null(result.SniffPath);
    }

    [Fact]
    public void Parse_SniffWithCsPath_ReturnsSniffMode()
    {
        CliArgs result = CliArgs.Parse(new[] { "sniff", @"D:\Repos\Sample.cs" });

        Assert.Equal(CliMode.Sniff, result.Mode);
        Assert.Equal(@"D:\Repos\Sample.cs", result.SniffPath);
    }

    [Theory]
    [InlineData("SNIFF")]
    [InlineData("Sniff")]
    public void Parse_SniffIsCaseInsensitive(string command)
    {
        CliArgs result = CliArgs.Parse(new[] { command, "Program.cs" });

        Assert.Equal(CliMode.Sniff, result.Mode);
        Assert.Equal("Program.cs", result.SniffPath);
    }

    [Fact]
    public void Parse_SniffStripsSurroundingQuotes()
    {
        CliArgs result = CliArgs.Parse(new[] { "sniff", "\"C:\\Work\\Foo.cs\"" });

        Assert.Equal(@"C:\Work\Foo.cs", result.SniffPath);
    }

    [Fact]
    public void Parse_UnknownCommand_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => CliArgs.Parse(new[] { "audit", "File.cs" }));

        Assert.Contains("Unknown command", ex.Message);
    }

    [Fact]
    public void Parse_SniffMissingPath_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => CliArgs.Parse(new[] { "sniff" }));

        Assert.Contains("requires a path", ex.Message);
    }

    [Fact]
    public void Parse_SniffNonCsExtension_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => CliArgs.Parse(new[] { "sniff", "readme.md" }));

        Assert.Contains(".cs file", ex.Message);
    }

    [Fact]
    public void Parse_SniffExtraArgs_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => CliArgs.Parse(new[] { "sniff", "A.cs", "B.cs" }));

        Assert.Contains("exactly one path", ex.Message);
    }
}
