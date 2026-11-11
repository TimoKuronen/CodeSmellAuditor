namespace CodeSmellAuditor.Core;

/// <summary>
/// Token counts reported by Ollama for one audit call, with the prompt size sent and the context
/// window requested, so the chars-per-token ratio and context headroom can be measured.
/// </summary>
public record AuditTokenUsage(int PromptTokens, int OutputTokens, int PromptChars, int NumCtx)
{
    public double CharsPerToken => PromptTokens > 0 ? (double)PromptChars / PromptTokens : 0;

    public int ContextHeadroomTokens => NumCtx - PromptTokens - OutputTokens;
}
