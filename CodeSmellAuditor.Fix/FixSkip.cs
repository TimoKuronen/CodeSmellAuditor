namespace CodeSmellAuditor.Fix;

/// <summary>
/// A violation a fixer recognised but left alone, with the reason it was not safe to change.
/// </summary>
public sealed record FixSkip(string RuleId, int Line, string Symbol, string Reason);
