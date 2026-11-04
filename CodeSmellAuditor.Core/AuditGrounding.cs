namespace CodeSmellAuditor.Core;

/// <summary>
/// Final verdict for a model report after its findings are checked against the audited source.
/// </summary>
/// <remarks>
/// Outcome rules, in order:
/// 1. Missing or invalid Status line: ReviewRequired (fail closed).
/// 2. Findings section missing: the model Status decides (legacy report shape).
/// 3. Otherwise: ReviewRequired when at least one finding is grounded, Passed when none are,
///    whatever the model Status says. Ungrounded findings are model noise and never fail a run.
/// </remarks>
public record AuditGrounding(
    string? ModelStatus,
    AuditOutcome Outcome,
    bool FindingsSectionFound,
    IReadOnlyList<AuditFinding> Findings)
{
    public int GroundedCount => Findings.Count(f => f.IsGrounded);

    public int UngroundedCount => Findings.Count - GroundedCount;

    public static AuditGrounding Evaluate(
        string reportBody,
        string findingsHeading,
        IReadOnlyList<SourceFile> sources)
    {
        string? modelStatus = AuditReportParser.ParseStatusValue(reportBody);
        IReadOnlyList<AuditFinding>? parsed = AuditFindingParser.Parse(reportBody, findingsHeading);
        IReadOnlyList<AuditFinding> findings = parsed is null
            ? Array.Empty<AuditFinding>()
            : FindingGroundingValidator.Validate(parsed, sources);

        AuditOutcome outcome;
        if (modelStatus is null)
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

        return new AuditGrounding(modelStatus, outcome, parsed is not null, findings);
    }
}
