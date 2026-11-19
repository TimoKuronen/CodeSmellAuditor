namespace CodeSmellAuditor.Fix;

/// <summary>
/// The rewritten file text plus what changed and what was skipped.
/// </summary>
public sealed record FixerOutput(string Content, IReadOnlyList<FixChange> Changes, IReadOnlyList<FixSkip> Skips)
{
    public static FixerOutput Unchanged(string content, IReadOnlyList<FixSkip>? skips = null) =>
        new(content, Array.Empty<FixChange>(), skips ?? Array.Empty<FixSkip>());
}
