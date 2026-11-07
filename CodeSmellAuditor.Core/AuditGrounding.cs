namespace CodeSmellAuditor.Core;

/// <summary>
/// Final verdict for a model report after its findings are checked against the audited source.
/// </summary>
/// <remarks>
/// Outcome rules, in order:
/// 1. Missing or invalid Status line: ReviewRequired (fail closed).
/// 2. Output limit hit before the findings section ended: ReviewRequired (fail closed).
/// 3. Findings section missing: the model Status decides (legacy report shape).
/// 4. Otherwise: ReviewRequired when at least one finding is grounded, Passed when none are,
///    whatever the model Status says. Ungrounded findings are model noise and never fail a run.
/// </remarks>
public record AuditGrounding(
    string? ModelStatus,
    AuditOutcome Outcome,
    bool FindingsSectionFound,
    IReadOnlyList<AuditFinding> Findings,
    bool FindingsTruncated = false)
{
    public int GroundedCount => Findings.Count(f => f.IsGrounded);

    public int UngroundedCount => Findings.Count - GroundedCount;

    public static AuditGrounding Evaluate(
        string reportBody,
        string findingsHeading,
        IReadOnlyList<SourceFile> sources,
        bool truncated = false)
    {
        string? modelStatus = AuditReportParser.ParseStatusValue(reportBody);
        IReadOnlyList<AuditFinding>? parsed = AuditFindingParser.Parse(reportBody, findingsHeading);
        IReadOnlyList<AuditFinding> findings = parsed is null
            ? Array.Empty<AuditFinding>()
            : FindingGroundingValidator.Validate(parsed, sources);

        bool findingsTruncated = truncated && !AuditFindingParser.IsSectionClosed(reportBody, findingsHeading);

        AuditOutcome outcome;
        if (modelStatus is null || findingsTruncated)
        {
            outcome = AuditOutcome.ReviewRequired;
        }
        else if (parsed is null)
        {
            outcome = modelStatus == AuditReportParser.CompliantStatus
                ? AuditOutcome.Passed
                : AuditOutcome.ReviewRequired;
        }
        else
        {
            outcome = findings.Any(f => f.IsGrounded) ? AuditOutcome.ReviewRequired : AuditOutcome.Passed;
        }

        return new AuditGrounding(modelStatus, outcome, parsed is not null, findings, findingsTruncated);
    }
}
