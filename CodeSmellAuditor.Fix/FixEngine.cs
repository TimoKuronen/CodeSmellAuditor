namespace CodeSmellAuditor.Fix;

/// <summary>
/// Plans fixes for a set of files. Every fixer result is checked before it is accepted: the file must
/// compile with the same number of errors as before, and running the fixer again must change nothing.
/// A result that fails a check is dropped and the file keeps its original content.
/// </summary>
public sealed class FixEngine
{
    private readonly IReadOnlyList<IStyleFixer> _fixers;

    public FixEngine(IEnumerable<IStyleFixer> fixers)
    {
        _fixers = fixers.ToList();
    }

    public async Task<IReadOnlyList<FileFix>> PlanAsync(IEnumerable<string> files, CancellationToken cancellationToken = default)
    {
        var plans = new List<FileFix>();
        foreach (string path in files)
        {
            plans.Add(await PlanFileAsync(path, cancellationToken));
        }

        return plans;
    }

    private async Task<FileFix> PlanFileAsync(string path, CancellationToken cancellationToken)
    {
        LoadedSource source;
        try
        {
            source = LoadedSource.Load(path);
        }
        catch (IOException ex)
        {
            return new FileFix(new LoadedSource(path, string.Empty, System.Text.Encoding.UTF8, string.Empty), string.Empty, Array.Empty<FixChange>(), Array.Empty<FixSkip>(), ex.Message);
        }

        string content = source.Content;
        var changes = new List<FixChange>();
        var skips = new List<FixSkip>();
        string? error = null;

        foreach (IStyleFixer fixer in _fixers)
        {
            try
            {
                FixerOutput output = await fixer.FixAsync(new FixInput(path, content), cancellationToken);
                skips.AddRange(output.Skips);

                if (output.Content == content)
                {
                    continue;
                }

                string? rejection = await RejectionReasonAsync(fixer, path, content, output, cancellationToken);
                if (rejection is not null)
                {
                    error = $"{fixer.RuleId}: {rejection}";
                    continue;
                }

                content = output.Content;
                changes.AddRange(output.Changes);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                error = $"{fixer.RuleId}: {ex.Message}";
            }
        }

        return new FileFix(source, content, changes, skips, error);
    }

    private static async Task<string?> RejectionReasonAsync(
        IStyleFixer fixer,
        string path,
        string before,
        FixerOutput output,
        CancellationToken cancellationToken)
    {
        int errorsBefore = await SemanticFile.CountErrorsAsync(path, before, cancellationToken);
        int errorsAfter = await SemanticFile.CountErrorsAsync(path, output.Content, cancellationToken);
        if (errorsAfter != errorsBefore)
        {
            return $"rejected, compiler errors changed from {errorsBefore} to {errorsAfter}";
        }

        FixerOutput second = await fixer.FixAsync(new FixInput(path, output.Content), cancellationToken);
        return second.Content == output.Content ? null : "rejected, a second run changed the result again";
    }
}
