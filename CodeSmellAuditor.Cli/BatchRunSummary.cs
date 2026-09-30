using System.Text;
using CodeSmellAuditor.Core;

namespace CodeSmellAuditor.Cli;

/// <summary>
/// Formats a plain-text batch pass/fail summary for the console host.
/// </summary>
public static class BatchRunSummary
{
    public static string Format(AuditRunResult result)
    {
        if (result.FileResults.Count == 0)
        {
            return "Batch summary: 0 files audited.";
        }

        int passed = result.FileResults.Count(file => file.Outcome == AuditOutcome.Passed);
        int reviewRequired = result.FileResults.Count(file => file.Outcome == AuditOutcome.ReviewRequired);
        int failed = result.FileResults.Count(file => file.Outcome == AuditOutcome.Failed);

        var builder = new StringBuilder();
        builder.Append("Batch summary: ");
        builder.Append(result.FileResults.Count);
        builder.Append(" files - ");
        builder.Append(passed);
        builder.Append(" passed, ");
        builder.Append(reviewRequired);
        builder.Append(" review required, ");
        builder.Append(failed);
        builder.Append(" failed");

        foreach (FileAuditResult file in result.FileResults)
        {
            builder.AppendLine();
            builder.Append("  ");
            builder.Append(FormatOutcome(file.Outcome));
            builder.Append("  ");
            builder.Append(file.FileName);
        }

        return builder.ToString();
    }

    private static string FormatOutcome(AuditOutcome outcome) => outcome switch
    {
        AuditOutcome.Passed => "PASS",
        AuditOutcome.ReviewRequired => "FAIL",
        AuditOutcome.Failed => "ERROR",
        _ => "UNKNOWN"
    };
}
