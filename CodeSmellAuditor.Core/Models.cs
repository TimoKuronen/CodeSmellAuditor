using System.Text.Json.Serialization;

namespace CodeSmellAuditor.Core;

// Core Domain Models
public record SourceFile(string FilePath, string Content);

public record AuditRule(string Name, string PromptGuideline);

public record AuditReport(string FilePath, string MarkdownCritique, bool HasPassed);

// Ollama API DTOs (Data Transfer Objects)
public class OllamaChatRequest
{
    public string Model { get; set; } = string.Empty;
    public bool Stream { get; set; }
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