namespace CodeSmellAuditor.Fix;

/// <summary>
/// One edit a fixer made, reported against the line it started on.
/// </summary>
public sealed record FixChange(string RuleId, int Line, string Description);
