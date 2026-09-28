using System.Text;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Prompt assembly for multi-file architecture audits. Kept separate from
/// <see cref="AuditPromptBuilder"/> so single-file budget and template stay unchanged.
/// </summary>
public static class SystemAuditPromptBuilder
{
    private const string GroundingRules = """
        Grounding rules:
        - Only report a finding if a concrete symbol, member, or pattern for it is visible in the provided source files.
        - Do not assume frameworks, infrastructure, or patterns not referenced in the provided sources (Unity types, UniTask, ScriptableObject, DI containers, service locators) unless the sources actually name them.
        - Claims that depend on code outside the audited set belong under Per-File Notes or Recommended Next Step as open questions, never as asserted Cross-File Findings.
        - Prefer findings that name the files and exact members involved.
        """;

    private const string OutputTemplate = """
        Output ONLY the final report in this exact format. No analysis traces, no reasoning, no preamble.

        # System Audit Report

        Status: COMPLIANT
        Score: 85

        ## Manifest Conformance
        - (max 5 bullets, one line each; state N/A if no manifest was provided)

        ## Cross-File Findings
        - (max 5 bullets, one line each)

        ## Per-File Notes
        - (max 5 bullets, one line each; name the file in each bullet)

        ## Recommended Next Step
        One sentence.

        Contract rules:
        - The Status line must appear exactly once and use EXACTLY one of: COMPLIANT or REVIEW REQUIRED (nothing else on that line after Status:).
        - The Score line must appear exactly once as an integer from 0 to 100.
        - Score is a rough qualitative signal for this run only, not a regression metric; do not imply meaningful change between runs from a small Score delta; base Score primarily on the count and severity of this run's own findings.
        - Emit Status, Score, Manifest Conformance, and Cross-File Findings first. If the output budget is tight, omit Per-File Notes and/or Recommended Next Step rather than truncating or omitting Status.
        - Judge the files against the stated architecture contract and the governance rules. Do not invent a different architecture; flag conflicts with the given contract.
        """;

    public static string BuildSystemPrompt(IEnumerable<AuditRule> rules)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are an automated C# architecture auditor.");
        builder.AppendLine("Evaluate a set of related source files for cross-file consistency against these governance rules:");
        builder.AppendLine();

        foreach (var rule in rules)
        {
            builder.AppendLine($"[RULE: {rule.Name}]");
            builder.AppendLine(rule.PromptGuideline);
            builder.AppendLine();
        }

        builder.AppendLine(GroundingRules);
        builder.AppendLine();
        builder.AppendLine(OutputTemplate);
        return builder.ToString();
    }

    public static string BuildUserPrompt(IReadOnlyList<SourceFile> files, string? manifestText)
    {
        if (files.Count < 2)
        {
            throw new ArgumentException("System audit requires at least two source files.", nameof(files));
        }

        var builder = new StringBuilder();
        builder.AppendLine($"File count: {files.Count}");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(manifestText))
        {
            builder.AppendLine("## Architecture Manifest");
            builder.AppendLine(manifestText.Trim());
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine("## Architecture Manifest");
            builder.AppendLine("(none provided - judge only against the governance rules and obvious cross-file inconsistencies)");
            builder.AppendLine();
        }

        builder.AppendLine("## Source Files");
        foreach (SourceFile file in files)
        {
            string fileName = GetDisplayFileName(file.FilePath);
            builder.AppendLine($"--- File: {fileName} ---");
            builder.AppendLine("```csharp");
            builder.AppendLine(file.Content);
            builder.AppendLine("```");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Path.GetFileName is OS-sensitive; normalize separators so Windows-style paths
    /// still yield a short file name when running on Linux (e.g. CI).
    /// </summary>
    public static string GetDisplayFileName(string filePath)
    {
        string normalized = filePath.Replace('\\', '/');
        return Path.GetFileName(normalized);
    }

    public static void ValidateBudget(string systemPrompt, string userPrompt, AuditConfiguration config)
    {
        AuditPromptBuilder.ValidateBudget(systemPrompt, userPrompt, config);
    }
}
