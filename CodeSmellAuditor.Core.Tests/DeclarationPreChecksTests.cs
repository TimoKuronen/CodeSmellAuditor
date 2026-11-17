using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class DeclarationPreChecksTests
{
    private static IReadOnlyList<PreCheckFinding> Run(string content, string path = "Sample.cs") =>
        SourcePreChecks.Run(new SourceFile(path, content));

    private static IEnumerable<PreCheckFinding> Only(IReadOnlyList<PreCheckFinding> findings, string check) =>
        findings.Where(f => f.Check == check);

    [Fact]
    public void StaticMutableField_InNonStaticClass_IsFlagged()
    {
        const string source = """
            public class Sample
            {
                private static int counter;
                public static List<int> Cache = new();
                private static readonly object Gate = new();
                public const int Limit = 3;
            }
            """;

        IReadOnlyList<PreCheckFinding> hits = Only(Run(source), SourcePreChecks.StaticMutableField).ToList();

        Assert.Equal(new[] { 3, 4 }, hits.Select(f => f.Line));
        Assert.Contains("'counter'", hits[0].Message);
    }

    [Fact]
    public void StaticFields_InStaticClassAndStaticMembersThatAreNotFields_AreIgnored()
    {
        const string source = """
            public static class Sample
            {
                private static int counter;
            }
            """;

        Assert.Empty(Only(Run(source), SourcePreChecks.StaticMutableField));

        const string members = """
            public class Other
            {
                public static int Total { get; set; }
                public static int Double => Total * 2;
                public static void Reset() { }
                public static event Action Changed;
            }
            """;

        Assert.Empty(Only(Run(members, "Other.cs"), SourcePreChecks.StaticMutableField));
    }

    [Fact]
    public void StaticField_WithAttributeAndLambdaInitializer_IsFlaggedOnItsOwnLine()
    {
        const string source = """
            public class Sample
            {
                [ThreadStatic]
                private static Action handler = () => { Run(); };

                private int Run() { return 1; }
            }
            """;

        PreCheckFinding hit = Assert.Single(Only(Run(source), SourcePreChecks.StaticMutableField));
        Assert.Equal(4, hit.Line);
    }

    [Fact]
    public void LocalVariables_AreNotFields()
    {
        const string source = """
            public class Sample
            {
                public void Run()
                {
                    int Count = 0;
                    Count++;
                }
            }
            """;

        Assert.Empty(Run(source));
    }

    [Theory]
    [InlineData("private int Count;", true)]
    [InlineData("int Count;", true)]
    [InlineData("private int m_count;", true)]
    [InlineData("private int count;", false)]
    [InlineData("private int _count;", false)]
    [InlineData("public int Count;", false)]
    [InlineData("protected int Count;", false)]
    [InlineData("private const int Count = 1;", false)]
    [InlineData("private static readonly int Count = 1;", false)]
    public void PrivateFieldNaming_FlagsOnlyPrivateInstanceFieldsThatAreNotCamelCase(string field, bool flagged)
    {
        string source = $$"""
            public class Sample
            {
                {{field}}
            }
            """;

        Assert.Equal(flagged, Only(Run(source), SourcePreChecks.PrivateFieldNaming).Any());
    }

    [Fact]
    public void PrivateFieldNaming_ReadsGenericTypesAndSeveralDeclarators()
    {
        const string source = """
            public class Sample
            {
                [SerializeField] private Dictionary<string, int> Map = new();
                private int first, Second;
            }
            """;

        IReadOnlyList<PreCheckFinding> hits = Only(Run(source), SourcePreChecks.PrivateFieldNaming).ToList();

        Assert.Equal(new[] { 3, 4 }, hits.Select(f => f.Line));
        Assert.Contains("'Map'", hits[0].Message);
        Assert.Contains("'Second'", hits[1].Message);
    }

    [Fact]
    public void Constructor_WithMoreThanFiveParameters_IsFlagged()
    {
        const string source = """
            public class Sample
            {
                public Sample(
                    IA a,
                    IB b,
                    Dictionary<string, int> c,
                    Func<int, int> d = null,
                    IE e = default,
                    IF f = default)
                    : base(a)
                {
                }
            }
            """;

        PreCheckFinding hit = Assert.Single(Only(Run(source), SourcePreChecks.ConstructorTooLarge));
        Assert.Equal(3, hit.Line);
        Assert.Contains("6 parameters", hit.Message);
    }

    [Fact]
    public void Constructor_WithFiveOrFewerParametersOrMethodsOfSimilarShape_IsNotFlagged()
    {
        const string source = """
            public class Sample
            {
                public Sample(IA a, IB b, IC c, ID d, IE e) { }

                public void Many(int a, int b, int c, int d, int e, int f, int g) { }

                public static Sample Create(int a, int b, int c, int d, int e, int f) => new(null, null, null, null, null);
            }
            """;

        Assert.Empty(Only(Run(source), SourcePreChecks.ConstructorTooLarge));
    }

    [Fact]
    public void PrimaryConstructorOnClass_IsCountedButRecordsAreNot()
    {
        const string classSource = "public class Sample(IA a, IB b, IC c, ID d, IE e, IF f) { }";
        const string recordSource = "public record Sample(int A, int B, int C, int D, int E, int F);";

        Assert.Single(Only(Run(classSource), SourcePreChecks.ConstructorTooLarge));
        Assert.Empty(Only(Run(recordSource), SourcePreChecks.ConstructorTooLarge));
    }

    [Fact]
    public void EnumOnlyFile_IsFlaggedWhenNotPublic()
    {
        const string internalEnum = """
            namespace Demo;

            internal enum Mode { A, B }
            """;

        PreCheckFinding hit = Assert.Single(Only(Run(internalEnum, "Mode.cs"), SourcePreChecks.EnumOnlyFile));
        Assert.Equal(3, hit.Line);
    }

    [Fact]
    public void EnumOnlyFile_IsIgnoredWhenPublicOrNotAlone()
    {
        Assert.Empty(Only(Run("public enum Mode { A, B }", "Mode.cs"), SourcePreChecks.EnumOnlyFile));

        const string withOwner = """
            internal enum Mode { A, B }

            internal class Owner { }
            """;

        Assert.Empty(Only(Run(withOwner, "Mode.cs"), SourcePreChecks.EnumOnlyFile));
    }

    [Fact]
    public void RegionLines_DoNotLeakIntoMemberDeclarations()
    {
        const string source = """
            public class Sample
            {
            #region Fields
                private int count;
            #endregion
            }
            """;

        Assert.Empty(Run(source));
    }
}
