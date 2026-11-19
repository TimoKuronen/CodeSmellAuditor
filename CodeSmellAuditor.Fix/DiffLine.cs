namespace CodeSmellAuditor.Fix;

/// <summary>
/// One line of a diff. Gap marks skipped unchanged lines between two hunks.
/// </summary>
public sealed record DiffLine(DiffKind Kind, int? OldLine, int? NewLine, string Text);
