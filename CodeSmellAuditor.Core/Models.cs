using System.Text.Json.Serialization;

namespace CodeSmellAuditor.Core;

// Core Domain Models
public record SourceFile(string FilePath, string Content);

public record AuditRule(string Name, string PromptGuideline);

public record AuditReport(string FilePath, string MarkdownCritique, bool HasPassed);

/// <summary>
/// Synthetic report identity for a multi-file system audit (not a single source path).
/// </summary>
public static class SystemAuditIdentity
{
    public const string ReportBaseName = "SystemAudit";
}

// Ollama API DTOs (Data Transfer Objects)
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