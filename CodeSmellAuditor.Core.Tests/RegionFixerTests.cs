using CodeSmellAuditor.Core;
using CodeSmellAuditor.Fix;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class RegionFixerTests
{
    private static Task<FixerOutput> Fix(string source) =>
        new RegionFixer().FixAsync(new FixInput("Sample.cs", source), CancellationToken.None);

    [Fact]
    public async Task RemovesRegionAndEndregionLines_KeepsTheCode()
    {
        const string source = "public class Sample\n{\n    #region Helpers\n    private int Add(int a, int b) => a + b;\n    #endregion\n}\n";

        FixerOutput output = await Fix(source);

        FixChange change = Assert.Single(output.Changes);
        Assert.Equal(3, change.Line);
        Assert.Equal("public class Sample\n{\n    private int Add(int a, int b) => a + b;\n}\n", output.Content);
    }

    [Fact]
    public async Task KeepsRegionsWithTheFixedStructuralNames()
    {
        const string source = "public class Sample\n{\n    #region Lifecycle\n    void Awake() { }\n    #endregion\n}\n";

        FixerOutput output = await Fix(source);

        Assert.Empty(output.Changes);
        Assert.Equal(source, output.Content);
    }

    [Fact]
    public async Task RemovesNestedRegions_AndKeepsAStructuralRegionInside()
    {
        const string source = "class A\n{\n#region Outer\n#region Lifecycle\n    void Awake() { }\n#endregion\n#region Inner\n    int x;\n#endregion\n#endregion\n}\n";

        FixerOutput output = await Fix(source);

        Assert.Equal(2, output.Changes.Count);
        Assert.Equal("class A\n{\n#region Lifecycle\n    void Awake() { }\n#endregion\n    int x;\n}\n", output.Content);
    }

    [Fact]
    public async Task CollapsesTheBlankLineLeftBehindByARemovedRegion()
    {
        const string source = "class A\n{\n    int a;\n\n    #region Extra\n\n    int b;\n\n    #endregion\n\n    int c;\n}\n";

        FixerOutput output = await Fix(source);

        Assert.Equal("class A\n{\n    int a;\n\n    int b;\n\n    int c;\n}\n", output.Content);
    }

    [Fact]
    public async Task KeepsCrlfLineEndings()
    {
        const string source = "class A\r\n{\r\n    #region Extra\r\n    int b;\r\n    #endregion\r\n}\r\n";

        FixerOutput output = await Fix(source);

        Assert.Equal("class A\r\n{\r\n    int b;\r\n}\r\n", output.Content);
    }

    [Fact]
    public async Task RegionWithoutEndregion_IsSkippedNotChanged()
    {
        const string source = "class A\n{\n    #region Extra\n    int b;\n}\n";

        FixerOutput output = await Fix(source);

        Assert.Empty(output.Changes);
        FixSkip skip = Assert.Single(output.Skips);
        Assert.Equal(3, skip.Line);
        Assert.Equal(source, output.Content);
    }

    [Fact]
    public async Task RegionTextInsideAStringOrComment_IsNotTouched()
    {
        const string source = "class A\n{\n    string s = \"#region X\";\n    // #region Y\n}\n";

        FixerOutput output = await Fix(source);

        Assert.Empty(output.Changes);
        Assert.Equal(source, output.Content);
    }

    [Fact]
    public async Task SecondRunChangesNothing()
    {
        const string source = "class A\n{\n    #region One\n    int a;\n    #endregion\n\n    #region Two\n    int b;\n    #endregion\n}\n";

        FixerOutput first = await Fix(source);
        FixerOutput second = await Fix(first.Content);

        Assert.Empty(second.Changes);
        Assert.Equal(first.Content, second.Content);
    }

    [Fact]
    public async Task FixedOutput_IsNotFlaggedByTheRegionPreCheck()
    {
        const string source = "class A\n{\n    #region One\n    int a;\n    #endregion\n    #region Lifecycle\n    void Awake() { }\n    #endregion\n}\n";

        FixerOutput output = await Fix(source);

        Assert.DoesNotContain(
            SourcePreChecks.Run(new SourceFile("Sample.cs", output.Content), "Unity"),
            f => f.Check == SourcePreChecks.RegionBlock);
        Assert.Contains(
            SourcePreChecks.Run(new SourceFile("Sample.cs", source), "Unity"),
            f => f.Check == SourcePreChecks.RegionBlock);
    }
}
