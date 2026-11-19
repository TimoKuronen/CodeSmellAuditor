namespace CodeSmellAuditor.Fix;

/// <summary>
/// Rewrites one file to satisfy one style rule. A fixer is deterministic and must be idempotent:
/// running it on its own output changes nothing.
/// </summary>
public interface IStyleFixer
{
    /// <summary>
    /// Matches the pre-check name of the rule it enforces.
    /// </summary>
    string RuleId { get; }

    Task<FixerOutput> FixAsync(FixInput input, CancellationToken cancellationToken);
}
