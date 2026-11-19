using CodeSmellAuditor.Fix;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class PrivateFieldNamingFixerTests
{
    private static Task<FixerOutput> Fix(string source) =>
        new PrivateFieldNamingFixer().FixAsync(new FixInput("Sample.cs", source), CancellationToken.None);

    [Fact]
    public async Task RenamesFieldAndEveryReference()
    {
        const string source = """
            public class Sample
            {
                private int Retry_Count;

                public void Run()
                {
                    Retry_Count++;
                    int doubled = Retry_Count * 2;
                    System.Console.WriteLine(nameof(Retry_Count) + doubled);
                }
            }
            """;

        FixerOutput output = await Fix(source);

        FixChange change = Assert.Single(output.Changes);
        Assert.Equal(3, change.Line);
        Assert.DoesNotContain("Retry_Count", output.Content);
        Assert.Contains("private int retryCount;", output.Content);
        Assert.Contains("retryCount++;", output.Content);
        Assert.Contains("nameof(retryCount)", output.Content);
    }

    [Fact]
    public async Task ConstructorParameterWithTheNewName_KeepsFieldAssignmentCorrect()
    {
        const string source = """
            public class Sample
            {
                private int Health;

                public Sample(int health)
                {
                    Health = health;
                }
            }
            """;

        FixerOutput output = await Fix(source);

        Assert.Single(output.Changes);
        Assert.Contains("this.health = health;", output.Content);
    }

    [Fact]
    public async Task OnlyTouchesTheFieldsInTheType_NotSameNamedMembersElsewhere()
    {
        const string source = """
            public class First
            {
                private int Count;
                public int Read() => Count;
            }

            public class Second
            {
                public int Count;
                public int Read() => Count;
            }
            """;

        FixerOutput output = await Fix(source);

        Assert.Single(output.Changes);
        Assert.Contains("private int count;", output.Content);
        Assert.Contains("public int Count;", output.Content);
    }

    [Theory]
    [InlineData("[SerializeField] private int Health;")]
    [InlineData("[UnityEngine.SerializeField] private int Health;")]
    [InlineData("[FormerlySerializedAs(\"x\")] private int Health;")]
    [InlineData("[JsonProperty] private int Health;")]
    public async Task FieldsWithPossibleSerializationContracts_AreSkippedWithAReason(string declaration)
    {
        string source = $$"""
            public class Sample
            {
                {{declaration}}
            }
            """;

        FixerOutput output = await Fix(source);

        Assert.Empty(output.Changes);
        Assert.Equal(source, output.Content);
        FixSkip skip = Assert.Single(output.Skips);
        Assert.Equal("Health", skip.Symbol);
        Assert.Contains("serialization", skip.Reason);
    }

    [Fact]
    public async Task InjectAndNonSerializedAttributes_DoNotBlockTheRename()
    {
        const string source = """
            public class Sample
            {
                [Inject] private object Service;
                [System.NonSerialized] private int Cache;
            }
            """;

        FixerOutput output = await Fix(source);

        Assert.Equal(2, output.Changes.Count);
        Assert.Contains("private object service;", output.Content);
        Assert.Contains("private int cache;", output.Content);
    }

    [Fact]
    public async Task KeywordCollisionAndExistingMember_AreSkipped()
    {
        const string source = """
            public class Sample
            {
                private object Lock;
                private int Health;
                public int health;
            }
            """;

        FixerOutput output = await Fix(source);

        Assert.Empty(output.Changes);
        Assert.Equal(2, output.Skips.Count);
        Assert.Contains(output.Skips, s => s.Symbol == "Lock" && s.Reason.Contains("identifier"));
        Assert.Contains(output.Skips, s => s.Symbol == "Health" && s.Reason.Contains("already exists"));
    }

    [Fact]
    public async Task PartialType_IsSkippedBecauseOtherFilesCannotBeUpdated()
    {
        const string source = """
            public partial class Sample
            {
                private int Health;
            }

            public partial class Sample
            {
                public int Read() => Health;
            }
            """;

        FixerOutput output = await Fix(source);

        Assert.Empty(output.Changes);
        Assert.Contains("partial", Assert.Single(output.Skips).Reason);
    }

    [Theory]
    [InlineData("public int Health;")]
    [InlineData("protected int Health;")]
    [InlineData("internal int Health;")]
    [InlineData("private static int Health;")]
    [InlineData("private const int Health = 1;")]
    [InlineData("private int health;")]
    [InlineData("private int _health;")]
    public async Task OtherFields_AreLeftAlone(string declaration)
    {
        string source = $$"""
            public class Sample
            {
                {{declaration}}
            }
            """;

        FixerOutput output = await Fix(source);

        Assert.Empty(output.Changes);
        Assert.Empty(output.Skips);
        Assert.Equal(source, output.Content);
    }

    [Fact]
    public async Task SeveralDeclarators_AreRenamedIndividually()
    {
        const string source = """
            public class Sample
            {
                private int first, Second, m_Third;
            }
            """;

        FixerOutput output = await Fix(source);

        Assert.Equal(2, output.Changes.Count);
        Assert.Contains("private int first, second, third;", output.Content);
    }

    [Fact]
    public async Task PreservesLineEndingsAndOtherText()
    {
        string source = "public class Sample\r\n{\r\n    // keeps this comment\r\n    private int Count;\r\n}\r\n";

        FixerOutput output = await Fix(source);

        Assert.Equal(source.Replace("Count", "count"), output.Content);
    }

    [Fact]
    public async Task SecondRunOnTheOutput_ChangesNothing()
    {
        const string source = """
            public class Sample
            {
                private int Retry_Count;
                private string m_Name;
                public Sample(string name) { m_Name = name; }
                public int Next() => ++Retry_Count;
            }
            """;

        FixerOutput first = await Fix(source);
        FixerOutput second = await Fix(first.Content);

        Assert.Equal(first.Content, second.Content);
        Assert.Empty(second.Changes);
    }

    [Fact]
    public async Task ReferencesThroughOtherInstancesNestedTypesAndLambdas_AreUpdated()
    {
        const string source = """
            public class Sample
            {
                private int Count;

                public bool Same(Sample other) => other.Count == Count;

                public System.Func<int> Reader() => () => Count;

                private class Inner
                {
                    public int Peek(Sample owner) => owner.Count;
                }
            }
            """;

        FixerOutput output = await Fix(source);

        Assert.Single(output.Changes);
        Assert.DoesNotContain("Count", output.Content);
        Assert.Contains("other.count == count", output.Content);
        Assert.Contains("() => count", output.Content);
        Assert.Contains("owner.count", output.Content);
    }

    [Fact]
    public async Task FixedOutput_IsNotFlaggedByThePrivateFieldNamingPreCheck()
    {
        const string source = """
            public class Sample
            {
                private int Retry_Count;
                private string m_Name;
                private float s_Speed;
                private object k_Lock2;
                private int HPBar, Other;
                [SerializeField] private int Kept;
            }
            """;

        FixerOutput output = await Fix(source);

        IEnumerable<PreCheckFinding> remaining = SourcePreChecks
            .Run(new SourceFile("Sample.cs", output.Content))
            .Where(f => f.Check == SourcePreChecks.PrivateFieldNaming);
        PreCheckFinding left = Assert.Single(remaining);
        Assert.Contains("'Kept'", left.Message);
        FixSkip skip = Assert.Single(output.Skips);
        Assert.Equal("Kept", skip.Symbol);
    }
}
