namespace CodeSmellAuditor.Core;

/// <param name="FailureReason">One-line cause when <paramref name="Outcome"/> is Failed (no report is saved then).</param>
public record FileAuditResult(string FileName, string ReportPath, AuditOutcome Outcome, string? FailureReason = null)
{
    public bool HasPassed => Outcome == AuditOutcome.Passed;
}

public record AuditRunResult(IReadOnlyList<FileAuditResult> FileResults)
{
    public bool AllPassed => FileResults.Count > 0 && FileResults.All(result => result.HasPassed);

    /// <summary>
    /// Process exit code: 0 passed, 1 review required, 2 tool/wiring failure.
    /// Empty runs exit 1 (nothing to pass). Any Failed dominates over ReviewRequired.
    /// </summary>
    public int ExitCode
    {
        get
        {
            if (FileResults.Count == 0)
            {
                return 1;
            }

            if (FileResults.Any(result => result.Outcome == AuditOutcome.Failed))
            {
                return 2;
            }

            if (FileResults.Any(result => result.Outcome == AuditOutcome.ReviewRequired))
            {
                return 1;
            }

            return 0;
        }
    }
}
