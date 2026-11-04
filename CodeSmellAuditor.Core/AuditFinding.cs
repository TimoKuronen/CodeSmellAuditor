namespace CodeSmellAuditor.Core;

/// <summary>
/// One model finding parsed from a report findings section, plus the result of checking
/// its quoted evidence against the audited source.
/// </summary>
public record AuditFinding(
    string Id,
    string? File,
    int? ReportedLine,
    string? Rule,
    string? Fix,
    string Evidence,
    string Issue)
{
    public bool IsGrounded { get; init; }

    /// <summary>
    /// 1-based line where the evidence was found; null when ungrounded or matched across lines.
    /// </summary>
    public int? VerifiedLine { get; init; }

    /// <summary>
    /// Why the finding is ungrounded, or a line-drift remark for a grounded one.
    /// </summary>
    public string? GroundingNote { get; init; }
}
