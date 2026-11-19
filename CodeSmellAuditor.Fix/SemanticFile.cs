using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeSmellAuditor.Fix;

/// <summary>
/// A single file loaded into an in-memory Roslyn project that references the .NET runtime only.
/// Types from other assemblies stay unresolved, which is enough for binding names inside the file.
/// </summary>
internal sealed class SemanticFile : IDisposable
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> RuntimeReferences = new(LoadRuntimeReferences);

    private readonly AdhocWorkspace _workspace;

    private SemanticFile(AdhocWorkspace workspace, Document document)
    {
        _workspace = workspace;
        Document = document;
    }

    public Document Document { get; }

    public static SemanticFile Create(string path, string content)
    {
        var workspace = new AdhocWorkspace();
        ProjectInfo projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "FixProject",
            "FixProject",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            metadataReferences: RuntimeReferences.Value);

        Project project = workspace.AddProject(projectInfo);
        Document document = workspace.AddDocument(project.Id, Path.GetFileName(path), SourceText.From(content));
        return new SemanticFile(workspace, document);
    }

    /// <summary>
    /// Number of compiler errors, syntax and semantic, in the given text.
    /// </summary>
    public static async Task<int> CountErrorsAsync(string path, string content, CancellationToken cancellationToken)
    {
        using SemanticFile file = Create(path, content);
        Compilation? compilation = await file.Document.Project.GetCompilationAsync(cancellationToken);
        return compilation is null
            ? 0
            : compilation.GetDiagnostics(cancellationToken).Count(d => d.Severity == DiagnosticSeverity.Error);
    }

    public void Dispose() => _workspace.Dispose();

    private static IReadOnlyList<MetadataReference> LoadRuntimeReferences()
    {
        string? trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(trusted))
        {
            return new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        }

        return trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    }
}
