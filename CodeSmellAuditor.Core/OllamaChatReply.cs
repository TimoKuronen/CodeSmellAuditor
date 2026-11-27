namespace CodeSmellAuditor.Core;

/// <summary>
/// What the chat endpoint answered. <see cref="Error"/> is set when Ollama reported an error,
/// and <see cref="Truncated"/> when the output limit ended the reply. Token counts are null
/// when Ollama does not report them.
/// </summary>
public sealed record OllamaChatReply(
    string Content,
    bool Truncated = false,
    int? PromptTokens = null,
    int? OutputTokens = null,
    string? Error = null);
