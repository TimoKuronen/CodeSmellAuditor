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
        Assert.False(result.NonInteractive);
        Assert.Null(result.Model);
        Assert.Null(result.Storage);
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
    public void Parse_SniffExtraPaths_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => CliArgs.Parse(new[] { "sniff", "A.cs", "B.cs" }));

        Assert.Contains("exactly one path", ex.Message);
    }

    [Fact]
    public void Parse_BatchWithFlags_ParsesOptions()
    {
        CliArgs result = CliArgs.Parse(new[]
        {
            "--model", "qwen3.5:4b",
            "--storage", @"D:\Storage",
            "--non-interactive"
        });

        Assert.Equal(CliMode.Batch, result.Mode);
        Assert.Equal("qwen3.5:4b", result.Model);
        Assert.Equal(@"D:\Storage", result.Storage);
        Assert.True(result.NonInteractive);
    }

    [Fact]
    public void Parse_SniffWithFlagsBeforeAndAfter_ParsesAll()
    {
        CliArgs result = CliArgs.Parse(new[]
        {
            "--model", "llama3",
            "sniff",
            "Program.cs",
            "--storage", @"C:\WorkstationStorage",
            "--non-interactive"
        });

        Assert.Equal(CliMode.Sniff, result.Mode);
        Assert.Equal("Program.cs", result.SniffPath);
        Assert.Equal("llama3", result.Model);
        Assert.Equal(@"C:\WorkstationStorage", result.Storage);
        Assert.True(result.NonInteractive);
    }

    [Fact]
    public void Parse_ModelMissingValue_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => CliArgs.Parse(new[] { "--model" }));

        Assert.Contains("--model requires a value", ex.Message);
    }

    [Fact]
    public void Parse_UnknownOption_Throws()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => CliArgs.Parse(new[] { "--verbose" }));

        Assert.Contains("Unknown option", ex.Message);
    }
}
