namespace CodeSmellAuditor.Core;

public record AuditConfiguration(
    string ModelName = AuditConfiguration.DefaultModelName,
    int NumCtx = 8192,
    int NumPredict = 1200,
    int MaxInputCharacters = 24000)
{
    public const string DefaultModelName = "qwen3.5:4b";
}

public static class AuditPromptBuilder
{
    private const string OutputTemplate = """
        Output ONLY the final report in this exact format. No analysis traces, no reasoning, no preamble.

        # Audit Report: {filename}

        Status: COMPLIANT
        Score: 85

        ## Top Findings
        - (max 5 bullets, one line each)

        ## Missing Context
        - (max 3 bullets, one line each)

        ## Recommended Next Step
        One sentence.

        Contract rules:
        - The Status line must appear exactly once and use EXACTLY one of: COMPLIANT or REVIEW REQUIRED (nothing else on that line after Status:).
        - The Score line must appear exactly once as an integer from 0 to 100.
        - Emit Status, Score, and Top Findings first. If the output budget is tight, omit Missing Context and/or Recommended Next Step rather than truncating or omitting Status.
        """;

    public static string BuildSystemPrompt(IEnumerable<AuditRule> rules)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("You are an automated C# static analysis auditor.");
        builder.AppendLine("Evaluate the target source code against these governance rules:");
        builder.AppendLine();

        foreach (var rule in rules)
        {
            builder.AppendLine($"[RULE: {rule.Name}]");
            builder.AppendLine(rule.PromptGuideline);
            builder.AppendLine();
        }

        builder.AppendLine(OutputTemplate);
        return builder.ToString();
    }

    public static string BuildUserPrompt(SourceFile file)
    {
        string fileName = Path.GetFileName(file.FilePath);
        return $"Target File: {fileName}\n\nSource Code:\n```csharp\n{file.Content}\n```";
    }

    public static void ValidateBudget(string systemPrompt, string userPrompt, AuditConfiguration config)
    {
        int totalChars = systemPrompt.Length + userPrompt.Length;
        if (totalChars > config.MaxInputCharacters)
        {
            throw new InvalidOperationException(
                $"Audit input exceeds character budget ({totalChars} / {config.MaxInputCharacters}). " +
                "Use a smaller target file or fewer rules.");
        }
    }
}

public static class AuditReportParser
{
    /// <summary>
    /// Pass/fail from a dedicated Status line only. Missing or unparseable Status fails closed.
    /// </summary>
    public static bool ParsePassStatus(string reportBody)
    {
        if (string.IsNullOrWhiteSpace(reportBody))
        {
            return false;
        }

        foreach (string line in reportBody.Split('\n'))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string statusValue = trimmed["Status:".Length..].Trim();
            if (statusValue.Equals("REVIEW REQUIRED", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (statusValue.Equals("COMPLIANT", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Status line present but not an allowed value: fail closed.
            return false;
        }

        // No Status line (truncated or drifted report): fail closed.
        return false;
    }
}
