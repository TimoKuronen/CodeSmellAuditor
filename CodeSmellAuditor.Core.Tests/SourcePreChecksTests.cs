using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class SourcePreChecksTests
{
    private static IReadOnlyList<PreCheckFinding> Run(string content, string path = "Sample.cs") =>
        SourcePreChecks.Run(new SourceFile(path, content));

    [Fact]
    public void CleanFile_HasNoFindings()
    {
        const string source = """
            namespace Demo;

            public class Sample
            {
                public int Count { get; set; }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void TwoTopLevelTypes_AreFlagged()
    {
        const string source = """
            namespace Demo
            {
                public class Sample { }

                public enum Mode { A, B }
            }
            """;

        PreCheckFinding finding = Assert.Single(Run(source));
        Assert.Equal(SourcePreChecks.MultipleTopLevelTypes, finding.Check);
        Assert.Equal(5, finding.Line);
        Assert.Contains("Sample, Mode", finding.Message);
    }

    [Fact]
    public void NestedTypes_AreNotTopLevel()
    {
        const string source = """
            public class Sample
            {
                private enum Mode { A, B }

                private sealed class Helper { }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void RecordWithPositionalParameters_CountsAsType()
    {
        const string source = """
            public record Point(int X, int Y);

            public record Size(int W, int H);
            """;

        Assert.Contains(Run(source, "Point.cs"), f => f.Check == SourcePreChecks.MultipleTopLevelTypes);
    }

    [Fact]
    public void SingleTypeWithOtherFileName_IsFlagged()
    {
        PreCheckFinding finding = Assert.Single(Run("public class Sample { }", "Other.cs"));
        Assert.Equal(SourcePreChecks.FileNameMismatch, finding.Check);
    }

    [Theory]
    [InlineData("Sample.cs")]
    [InlineData("Sample.Generated.cs")]
    public void SingleTypeWithMatchingFileName_IsNotFlagged(string path)
    {
        Assert.Empty(Run("public partial class Sample { }", path));
    }

    [Fact]
    public void GenericTypeNameIgnoresTypeParameters()
    {
        Assert.Empty(Run("public class Repo<T> where T : class { }", "Repo.cs"));
    }

    [Fact]
    public void WipMarkersInComments_AreFlagged()
    {
        const string source = """
            public class Sample
            {
                // TODO: remove
                private int _x; /* FIXME later */
            }
            """;

        IReadOnlyList<PreCheckFinding> findings = Run(source);

        Assert.Equal(new[] { 3, 4 }, findings.Select(f => f.Line));
        Assert.All(findings, f => Assert.Equal(SourcePreChecks.WipMarker, f.Check));
    }

    [Fact]
    public void WipWordsInStrings_AreIgnored()
    {
        const string source = """
            public class Sample
            {
                private const string Label = "TODO list";
                private const string Raw = @"HACK
            FIXME";
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void DisabledBlockAndNotImplementedStub_AreFlagged()
    {
        const string source = """
            public class Sample
            {
            #if false
                public void Old() { }
            #endif
                public void Later() => throw new NotImplementedException();
            }
            """;

        IReadOnlyList<PreCheckFinding> findings = Run(source);

        Assert.Equal(new[] { 3, 6 }, findings.Select(f => f.Line));
    }

    [Fact]
    public void ConsecutiveCommentedOutCode_IsFlaggedOnce()
    {
        const string source = """
            public class Sample
            {
                // var total = Compute();
                // total++;
                // Log(total);
                public void Run() { }
            }
            """;

        PreCheckFinding finding = Assert.Single(Run(source));
        Assert.Equal(SourcePreChecks.CommentedOutCode, finding.Check);
        Assert.Equal(3, finding.Line);
        Assert.Contains("3 consecutive", finding.Message);
    }

    [Fact]
    public void SingleCommentedLineAndProse_AreNotFlagged()
    {
        const string source = """
            public class Sample
            {
                // total++;
                public void Run() { }

                /// <summary>Counts items; never negative.</summary>
                public int Count { get; }

                // Explains why the count is cached.
                // Not recomputed on each call.
            }
            """;

        Assert.Empty(Run(source));
    }

    [Fact]
    public void FindingsAreOrderedByLineWithSequentialIds()
    {
        const string source = """
            public class Sample
            {
                // HACK
                // a = 1;
                // b = 2;
            }

            public class Extra { }
            """;

        IReadOnlyList<PreCheckFinding> findings = Run(source);

        Assert.Equal(new[] { "P1", "P2", "P3" }, findings.Select(f => f.Id));
        Assert.Equal(findings.Select(f => f.Line).OrderBy(l => l), findings.Select(f => f.Line));
    }
}
