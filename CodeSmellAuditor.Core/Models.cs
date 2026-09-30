using System.Text.Json.Serialization;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Result of an audit attempt: model verdict or tool/wiring failure.
/// </summary>
public enum AuditOutcome
{
    Passed,
    ReviewRequired,
    Failed
}

public record SourceFile(string FilePath, string Content);

public record AuditRule(string Name, string PromptGuideline);

public record AuditReport(string FilePath, string MarkdownCritique, AuditOutcome Outcome)
{
    public bool HasPassed => Outcome == AuditOutcome.Passed;

    public static AuditReport FromParsedStatus(string filePath, string markdownCritique)
    {
        bool passed = AuditReportParser.ParsePassStatus(markdownCritique);
        return new AuditReport(
            filePath,
            markdownCritique,
            passed ? AuditOutcome.Passed : AuditOutcome.ReviewRequired);
    }

    public static AuditReport ToolFailure(string filePath, string markdownCritique) =>
        new(filePath, markdownCritique, AuditOutcome.Failed);
}

/// <summary>
/// Synthetic report identity for a multi-file system audit (not a single source path).
/// </summary>
public static class SystemAuditIdentity
{
    public const string ReportBaseName = "SystemAudit";
}

public class OllamaChatRequest
{
    public string Model { get; set; } = string.Empty;
    public bool Stream { get; set; }
    public bool Think { get; set; }
    public List<OllamaMessage> Messages { get; set; } = new();
    public Dictionary<string, object>? Options { get; set; }
}

public class OllamaMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("thinking")]
    public string? Thinking { get; set; }
}
