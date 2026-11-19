using System.Text;
using CodeSmellAuditor.Fix;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public sealed class FixEngineTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "csa-fix-" + Guid.NewGuid().ToString("N"));

    public FixEngineTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string Write(string name, string content, Encoding? encoding = null)
    {
        string path = Path.Combine(_folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
        return path;
    }

    private sealed class CallbackFixer : IStyleFixer
    {
        private readonly Func<string, string> _rewrite;

        public CallbackFixer(string ruleId, Func<string, string> rewrite)
        {
            RuleId = ruleId;
            _rewrite = rewrite;
        }

        public string RuleId { get; }

        public Task<FixerOutput> FixAsync(FixInput input, CancellationToken cancellationToken)
        {
            string content = _rewrite(input.Content);
            IReadOnlyList<FixChange> changes = content == input.Content
                ? Array.Empty<FixChange>()
                : new[] { new FixChange(RuleId, 1, "test change") };
            return Task.FromResult(new FixerOutput(content, changes, Array.Empty<FixSkip>()));
        }
    }

    [Fact]
    public async Task Plan_RenamesFieldsAndReportsChanges()
    {
        string path = Write("Sample.cs", "public class Sample\n{\n    private int Count;\n    public int Read() => Count;\n}\n");

        IReadOnlyList<FileFix> plan = await new FixEngine(FixerRegistry.CreateDefault()).PlanAsync(new[] { path });

        FileFix fix = Assert.Single(plan);
        Assert.True(fix.HasChanges);
        Assert.Contains("private int count;", fix.FixedContent);
        Assert.Null(fix.Error);
    }

    [Fact]
    public async Task Plan_AcceptsFilesWhoseTypesComeFromOtherAssemblies()
    {
        string path = Write(
            "Controller.cs",
            "using UnityEngine;\n\npublic class Controller : MonoBehaviour\n{\n" +
            "    [Inject] private IService Service;\n    private Transform Target;\n\n" +
            "    private void Update()\n    {\n        Service.Tick(Target.position);\n        Debug.Log(Target);\n    }\n}\n");

        FileFix fix = Assert.Single(await new FixEngine(FixerRegistry.CreateDefault()).PlanAsync(new[] { path }));

        Assert.Null(fix.Error);
        Assert.Equal(2, fix.Changes.Count);
        Assert.Contains("service.Tick(target.position);", fix.FixedContent);
        Assert.Contains("Debug.Log(target);", fix.FixedContent);
    }

    [Fact]
    public async Task Plan_RejectsAFixThatAddsCompilerErrors()
    {
        string path = Write("Sample.cs", "public class Sample\n{\n    private int Count;\n}\n");
        var breaking = new CallbackFixer("breaker", c => c.Replace("private int Count;", "private int Count"));

        FileFix fix = Assert.Single(await new FixEngine(new[] { breaking }).PlanAsync(new[] { path }));

        Assert.False(fix.HasChanges);
        Assert.Equal(fix.Source.Content, fix.FixedContent);
        Assert.Contains("compiler errors changed", fix.Error);
    }

    [Fact]
    public async Task Plan_RejectsAFixThatIsNotIdempotent()
    {
        string path = Write("Sample.cs", "public class Sample\n{\n    private int count;\n}\n");
        var growing = new CallbackFixer("grower", c => c.Replace("count", "countx"));

        FileFix fix = Assert.Single(await new FixEngine(new[] { growing }).PlanAsync(new[] { path }));

        Assert.False(fix.HasChanges);
        Assert.Contains("second run", fix.Error);
    }

    [Fact]
    public async Task Plan_KeepsGoodFixesWhenALaterFixerIsRejected()
    {
        string path = Write("Sample.cs", "public class Sample\n{\n    private int Count;\n}\n");
        var breaking = new CallbackFixer("breaker", c => c.Replace("private int count;", "private int count"));

        FileFix fix = Assert.Single(await new FixEngine(new IStyleFixer[] { new PrivateFieldNamingFixer(), breaking }).PlanAsync(new[] { path }));

        Assert.Contains("private int count;", fix.FixedContent);
        Assert.Single(fix.Changes);
        Assert.NotNull(fix.Error);
    }

    [Fact]
    public async Task Plan_FilesWithNothingToFix_HaveNoChanges()
    {
        string path = Write("Clean.cs", "public class Clean\n{\n    private int count;\n}\n");

        FileFix fix = Assert.Single(await new FixEngine(FixerRegistry.CreateDefault()).PlanAsync(new[] { path }));

        Assert.False(fix.HasChanges);
        Assert.Null(fix.Error);
    }

    [Fact]
    public async Task Plan_ReportsSkipsFromFixers()
    {
        string path = Write("Sample.cs", "public class Sample\n{\n    [SerializeField] private int Health;\n}\n");

        FileFix fix = Assert.Single(await new FixEngine(FixerRegistry.CreateDefault()).PlanAsync(new[] { path }));

        Assert.False(fix.HasChanges);
        Assert.Single(fix.Skips);
    }

    [Fact]
    public void Enumerator_ExpandsFoldersAndSkipsGeneratedAndBuildFolders()
    {
        Write("A.cs", "class A {}");
        Write("Sub/B.cs", "class B {}");
        Write("obj/C.cs", "class C {}");
        Write("Library/D.cs", "class D {}");
        Write("E.g.cs", "class E {}");
        Write("Form.Designer.cs", "class F {}");
        Write("notes.txt", "x");

        IReadOnlyList<string> files = SourceFileEnumerator.Expand(new[] { _folder });

        Assert.Equal(new[] { "A.cs", "B.cs" }, files.Select(Path.GetFileName).OrderBy(n => n));
    }

    [Fact]
    public void Enumerator_RejectsMissingPathsAndNonCsFiles()
    {
        string text = Write("notes.txt", "x");

        Assert.Throws<FileNotFoundException>(() => SourceFileEnumerator.Expand(new[] { Path.Combine(_folder, "missing") }));
        Assert.Throws<ArgumentException>(() => SourceFileEnumerator.Expand(new[] { text }));
    }

    [Fact]
    public async Task Apply_WritesFixKeepingBomAndLineEndings()
    {
        string path = Write("Sample.cs", "public class Sample\r\n{\r\n    private int Count;\r\n}\r\n", new UTF8Encoding(true));
        FileFix fix = Assert.Single(await new FixEngine(FixerRegistry.CreateDefault()).PlanAsync(new[] { path }));

        ApplyResult result = FixApplier.Apply(fix);

        Assert.True(result.Written);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        string text = new UTF8Encoding(false).GetString(bytes[3..]);
        Assert.Equal("public class Sample\r\n{\r\n    private int count;\r\n}\r\n", text);
    }

    [Fact]
    public async Task Apply_WithoutBom_DoesNotAddOne()
    {
        string path = Write("Sample.cs", "public class Sample\n{\n    private int Count;\n}\n");
        FileFix fix = Assert.Single(await new FixEngine(FixerRegistry.CreateDefault()).PlanAsync(new[] { path }));

        FixApplier.Apply(fix);

        Assert.NotEqual((byte)0xEF, File.ReadAllBytes(path)[0]);
    }

    [Fact]
    public async Task Apply_RefusesWhenTheFileChangedAfterPlanning()
    {
        string path = Write("Sample.cs", "public class Sample\n{\n    private int Count;\n}\n");
        FileFix fix = Assert.Single(await new FixEngine(FixerRegistry.CreateDefault()).PlanAsync(new[] { path }));
        File.AppendAllText(path, "// edited meanwhile\n");

        ApplyResult result = FixApplier.Apply(fix);

        Assert.False(result.Written);
        Assert.Contains("changed on disk", result.Reason);
        Assert.Contains("private int Count;", File.ReadAllText(path));
    }

    [Fact]
    public async Task Apply_OutsideGitCheckout_KeepsABackup()
    {
        string path = Write("Sample.cs", "public class Sample\n{\n    private int Count;\n}\n");
        FileFix fix = Assert.Single(await new FixEngine(FixerRegistry.CreateDefault()).PlanAsync(new[] { path }));

        FixApplier.Apply(fix);

        Assert.Contains("private int Count;", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public async Task Apply_SkipsFilesWithoutChanges()
    {
        string path = Write("Clean.cs", "public class Clean\n{\n    private int count;\n}\n");
        FileFix fix = Assert.Single(await new FixEngine(FixerRegistry.CreateDefault()).PlanAsync(new[] { path }));

        Assert.False(FixApplier.Apply(fix).Written);
        Assert.False(File.Exists(path + ".bak"));
    }
}
