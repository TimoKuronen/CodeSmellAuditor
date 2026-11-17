namespace CodeSmellAuditor.Eval;

/// <summary>
/// Fixed locations the benchmark reads and writes, all derived from the repository root.
/// </summary>
public sealed record EvalPaths(string Root)
{
    public string FixturesDir => Path.Combine(Root, "eval", "fixtures");

    public string RulesDir => Path.Combine(Root, "WorkstationStorage", "Rules");

    public string CoreDir => Path.Combine(Root, "CodeSmellAuditor.Core");

    public string BenchDir => Path.Combine(Root, ".agent", "benchmark");

    public string ResultsCsv => Path.Combine(BenchDir, "results.csv");

    public string ResultsMarkdown => Path.Combine(BenchDir, "results.md");

    public string RunsDir => Path.Combine(BenchDir, "runs");

    public static EvalPaths Locate(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return new EvalPaths(Path.GetFullPath(explicitRoot));
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CodeSmellAuditor.slnx")))
            {
                return new EvalPaths(current.FullName);
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root (CodeSmellAuditor.slnx) not found above the Eval binary; pass --root.");
    }
}
