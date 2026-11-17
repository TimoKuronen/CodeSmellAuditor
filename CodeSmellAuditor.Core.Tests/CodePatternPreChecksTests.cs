using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class CodePatternPreChecksTests
{
    private static IReadOnlyList<PreCheckFinding> Run(string content, string? stack = null) =>
        SourcePreChecks.Run(new SourceFile("Sample.cs", content), stack);

    private static IEnumerable<PreCheckFinding> Only(IReadOnlyList<PreCheckFinding> findings, string check) =>
        findings.Where(f => f.Check == check);

    [Fact]
    public void EmptyCatch_WithOrWithoutCommentAndFilter_IsFlagged()
    {
        const string source = """
            public class Sample
            {
                public void Run()
                {
                    try { Work(); }
                    catch (Exception) { }

                    try { Work(); }
                    catch
                    {
                        // ignored
                    }

                    try { Work(); }
                    catch (IOException ex) when (ex.HResult == 1) { }
                }
            }
            """;

        IReadOnlyList<PreCheckFinding> hits = Only(Run(source), SourcePreChecks.EmptyCatch).ToList();

        Assert.Equal(new[] { 6, 9, 15 }, hits.Select(f => f.Line));
    }

    [Fact]
    public void CatchThatHandlesOrRethrows_IsNotFlagged()
    {
        const string source = """
            public class Sample
            {
                public void Run()
                {
                    try { Work(); }
                    catch (Exception ex) { Log(ex); throw; }
                }
            }
            """;

        Assert.Empty(Only(Run(source), SourcePreChecks.EmptyCatch));
    }

    [Fact]
    public void ServiceLocator_IsFlaggedOnEveryStack()
    {
        const string source = """
            public class Sample
            {
                private readonly IClock clock = ServiceLocator.Resolve<IClock>();
            }
            """;

        PreCheckFinding hit = Assert.Single(Only(Run(source), SourcePreChecks.HiddenDependencyLookup));
        Assert.Equal(3, hit.Line);
    }

    [Fact]
    public void UnityLookups_AreFlaggedOnlyForTheUnityStack()
    {
        const string source = """
            public class Sample
            {
                private void Awake()
                {
                    player = FindObjectOfType<Player>();
                    hud = GameObject.Find("Hud");
                    sheet = Resources.Load<Sheet>("Sheet");
                    other = GetComponent<Other>();
                }
            }
            """;

        IReadOnlyList<PreCheckFinding> unity = Only(Run(source, "Unity"), SourcePreChecks.HiddenDependencyLookup).ToList();

        Assert.Equal(new[] { 5, 6, 7 }, unity.Select(f => f.Line));
        Assert.Empty(Only(Run(source), SourcePreChecks.HiddenDependencyLookup));
    }

    [Fact]
    public void LookupNamesInStringsAndComments_AreIgnored()
    {
        const string source = """
            public class Sample
            {
                // FindObjectOfType is banned here
                private const string Name = "GameObject.Find(";
            }
            """;

        Assert.Empty(Only(Run(source, "Unity"), SourcePreChecks.HiddenDependencyLookup));
    }

    [Fact]
    public void NonAsciiComments_AreReportedOncePerFile()
    {
        string source = "public class Sample\n{\n    // café résumé\n    // plain\n    /* naïve */\n    private int count;\n}\n";

        PreCheckFinding hit = Assert.Single(Only(Run(source), SourcePreChecks.NonAsciiComment));
        Assert.Equal(3, hit.Line);
        Assert.Contains("2 comment line(s)", hit.Message);
    }

    [Fact]
    public void NonAsciiInStringLiterals_IsIgnored()
    {
        string source = "public class Sample\n{\n    private const string Name = \"café\";\n}\n";

        Assert.Empty(Only(Run(source), SourcePreChecks.NonAsciiComment));
    }

    [Fact]
    public void Regions_WithTheFixedNames_AreNotFlagged()
    {
        const string source = """
            public class Sample
            {
            #region Lifecycle
                private void Awake() { }
            #endregion
            #region private helpers
                private void Help() { }
            #endregion
            }
            """;

        Assert.Empty(Only(Run(source, "Unity"), SourcePreChecks.RegionBlock));
    }

    [Fact]
    public void Regions_AreFlaggedOnlyForTheUnityStack()
    {
        const string source = """
            public class Sample
            {
            #region Fields
                private int count;
            #endregion
            }
            """;

        PreCheckFinding hit = Assert.Single(Only(Run(source, "Unity"), SourcePreChecks.RegionBlock));
        Assert.Equal(3, hit.Line);
        Assert.Empty(Only(Run(source), SourcePreChecks.RegionBlock));
    }
}
