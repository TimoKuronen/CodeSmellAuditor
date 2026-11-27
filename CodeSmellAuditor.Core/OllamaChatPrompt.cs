namespace CodeSmellAuditor.Core;

/// <summary>
/// One system and user message pair with the model settings to answer it.
/// </summary>
public sealed record OllamaChatPrompt(
    string Model,
    string SystemPrompt,
    string UserPrompt,
    int NumCtx,
    int NumPredict,
    double Temperature);
