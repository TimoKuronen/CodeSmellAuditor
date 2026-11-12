using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Renders grounding results as a markdown section appended to the report and as a compact
/// JSON sidecar that callers can read instead of the full report.
/// </summary>
public static class AuditSummaryFormatter
{
    /// <summary>
    /// Bumped when the sidecar shape changes incompatibly.
    /// </summary>
    public const int ContractVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Local file read by tools and people; keep backticks and generics readable.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    /// <summary>
    /// <c>X_{timestamp}_Critique.md</c> maps to <c>X_{timestamp}_Critique.summary.json</c>.
    /// </summary>
    public static string GetSummaryPath(string reportPath) =>
        Path.ChangeExtension(reportPath, ".summary.json");

    public static string FormatStatus(AuditOutcome outcome) => outcome switch
    {
        AuditOutcome.Passed => AuditReportParser.CompliantStatus,
        AuditOutcome.ReviewRequired => AuditReportParser.ReviewRequiredStatus,
        _ => "FAILED"
    };

    public static string BuildPreCheckSection(IReadOnlyList<PreCheckFinding> preChecks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Pre-checks");
        builder.AppendLine();

        if (preChecks.Count == 0)
        {
            builder.AppendLine("None.");
            return builder.ToString();
        }

        foreach (PreCheckFinding finding in preChecks)
        {
            builder.AppendLine($"- {finding.Id} {finding.Check} - line {finding.Line}: {finding.Message}");
        }

        return builder.ToString();
    }

    public static string BuildMarkdownSection(AuditGrounding grounding)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Grounding");
        builder.AppendLine();
        builder.AppendLine(
            $"Model status: {grounding.ModelStatus ?? "missing"} | " +
            $"Final status: {FormatStatus(grounding.Outcome)} | " +
            $"Grounded: {grounding.GroundedCount} | Ungrounded: {grounding.UngroundedCount}");

        if (grounding.FindingsTruncated)
        {
            builder.AppendLine();
            builder.AppendLine("Output limit reached before the findings ended; the report cannot pass.");
        }

        if (!grounding.FindingsSectionFound)
        {
            if (grounding.FindingsTruncated)
            {
                return builder.ToString();
            }

            builder.AppendLine();
            builder.AppendLine("Findings section not found; final status follows the model Status line.");
            return builder.ToString();
        }

        if (grounding.Findings.Count > 0)
        {
            builder.AppendLine();
        }

        foreach (AuditFinding finding in grounding.Findings)
        {
            string location = finding.VerifiedLine is { } line ? $"{finding.File}:{line}" : finding.File ?? "unknown file";
            string line1 = finding.IsGrounded
                ? $"- {finding.Id} grounded - {location}"
                : $"- {finding.Id} ungrounded - {finding.GroundingNote}";
            builder.AppendLine(finding.IsGrounded && finding.GroundingNote is not null
                ? $"{line1} ({finding.GroundingNote})"
                : line1);
        }

        return builder.ToString();
    }

    public static string BuildJson(
        string targetLabel,
        string reportFileName,
        AuditGrounding grounding,
        AuditTokenUsage? usage = null,
        IReadOnlyList<PreCheckFinding>? preChecks = null)
    {
        var summary = new SummaryDocument(
            ContractVersion,
            targetLabel,
            reportFileName,
            grounding.ModelStatus,
            FormatStatus(grounding.Outcome),
            grounding.FindingsSectionFound,
            grounding.FindingsTruncated,
            grounding.GroundedCount,
            grounding.UngroundedCount,
            grounding.Findings
                .Select(f => new SummaryFinding(
                    f.Id,
                    f.File,
                    f.VerifiedLine ?? f.ReportedLine,
                    f.Rule,
                    f.Fix,
                    f.IsGrounded,
                    f.Issue,
                    f.Evidence.Length > 0 ? f.Evidence : null,
                    f.GroundingNote))
                .ToList(),
            usage is null
                ? null
                : new SummaryUsage(
                    usage.PromptTokens,
                    usage.OutputTokens,
                    usage.PromptChars,
                    usage.NumCtx,
                    Math.Round(usage.CharsPerToken, 2),
                    usage.ContextHeadroomTokens),
            preChecks);

        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private sealed record SummaryDocument(
        int ContractVersion,
        string Target,
        string Report,
        string? ModelStatus,
        string Status,
        bool FindingsSectionFound,
        bool FindingsTruncated,
        int Grounded,
        int Ungrounded,
        IReadOnlyList<SummaryFinding> Findings,
        SummaryUsage? Usage,
        IReadOnlyList<PreCheckFinding>? PreChecks);

    private sealed record SummaryUsage(
        int PromptTokens,
        int OutputTokens,
        int PromptChars,
        int NumCtx,
        double CharsPerToken,
        int ContextHeadroomTokens);

    private sealed record SummaryFinding(
        string Id,
        string? File,
        int? Line,
        string? Rule,
        string? Fix,
        bool Grounded,
        string Issue,
        string? Evidence,
        string? Note);
}
