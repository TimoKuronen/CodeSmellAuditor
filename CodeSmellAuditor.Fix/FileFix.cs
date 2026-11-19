namespace CodeSmellAuditor.Fix;

/// <summary>
/// The planned outcome for one file. Error is set when a fix was rejected or the file could not be
/// processed; the content is then left unchanged.
/// </summary>
public sealed record FileFix(
    LoadedSource Source,
    string FixedContent,
    IReadOnlyList<FixChange> Changes,
    IReadOnlyList<FixSkip> Skips,
    string? Error = null)
{
    public bool HasChanges => Changes.Count > 0 && Error is null;
}
