namespace CodeSmellAuditor.Fix;

/// <summary>
/// Outcome of writing one planned fix to disk. Reason explains a file that was not written.
/// </summary>
public sealed record ApplyResult(string Path, bool Written, string? Reason = null);
