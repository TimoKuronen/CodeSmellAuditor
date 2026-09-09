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

        int passed = result.FileResults.Count(file => file.HasPassed);
        int failed = result.FileResults.Count - passed;

        var builder = new StringBuilder();
        builder.Append("Batch summary: ");
        builder.Append(result.FileResults.Count);
        builder.Append(" files - ");
        builder.Append(passed);
        builder.Append(" passed, ");
        builder.Append(failed);
        builder.Append(" failed");

        foreach (FileAuditResult file in result.FileResults)
        {
            builder.AppendLine();
            builder.Append("  ");
            builder.Append(file.HasPassed ? "PASS" : "FAIL");
            builder.Append("  ");
            builder.Append(file.FileName);
        }

        return builder.ToString();
    }
}
