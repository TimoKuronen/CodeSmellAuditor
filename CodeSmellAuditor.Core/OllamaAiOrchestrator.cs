namespace CodeSmellAuditor.Core;

public class OllamaAiOrchestrator : IAiOrchestrator
{
    private const double AuditTemperature = 0.2;

    private readonly OllamaChatClient _chatClient;
    private readonly AuditConfiguration _config;

    public OllamaAiOrchestrator(OllamaChatClient chatClient, AuditConfiguration config)
    {
        _chatClient = chatClient;
        _config = config;
    }

    public async Task<AuditReport> AnalyzeCodeAsync(
        SourceFile file,
        IEnumerable<AuditRule> rules,
        Action<string> onTokenReceived,
        CancellationToken cancellationToken = default)
    {
        string systemPrompt = AuditPromptBuilder.BuildSystemPrompt(rules);
        string userPrompt = AuditPromptBuilder.BuildUserPrompt(file);

        try
        {
            AuditPromptBuilder.ValidateBudget(systemPrompt, userPrompt, _config);
        }
        catch (InvalidOperationException ex)
        {
            return AuditReport.ToolFailure(
                file.FilePath,
                $"# Audit Budget Exceeded\n\n{ex.Message}");
        }

        return await CompleteChatAsync(
            file.FilePath,
            systemPrompt,
            userPrompt,
            onTokenReceived,
            cancellationToken);
    }

    public async Task<AuditReport> AnalyzeSystemAsync(
        IReadOnlyList<SourceFile> files,
        string? manifestText,
        IEnumerable<AuditRule> rules,
        Action<string> onTokenReceived,
        CancellationToken cancellationToken = default)
    {
        if (files.Count < 2)
        {
            return AuditReport.ToolFailure(
                SystemAuditIdentity.ReportBaseName,
                "# System Audit Error\n\nSystem audit requires at least two source files.");
        }

        string systemPrompt = SystemAuditPromptBuilder.BuildSystemPrompt(rules);
        string userPrompt = SystemAuditPromptBuilder.BuildUserPrompt(files, manifestText);

        try
        {
            SystemAuditPromptBuilder.ValidateBudget(systemPrompt, userPrompt, _config);
        }
        catch (InvalidOperationException ex)
        {
            return AuditReport.ToolFailure(
                SystemAuditIdentity.ReportBaseName,
                $"# Audit Budget Exceeded\n\n{ex.Message}");
        }

        return await CompleteChatAsync(
            SystemAuditIdentity.ReportBaseName,
            systemPrompt,
            userPrompt,
            onTokenReceived,
            cancellationToken);
    }

    private async Task<AuditReport> CompleteChatAsync(
        string reportPath,
        string systemPrompt,
        string userPrompt,
        Action<string> onTokenReceived,
        CancellationToken cancellationToken)
    {
        var prompt = new OllamaChatPrompt(
            _config.ModelName,
            systemPrompt,
            userPrompt,
            _config.NumCtx,
            _config.NumPredict,
            AuditTemperature);

        try
        {
            OllamaChatReply reply = await _chatClient.StreamAsync(prompt, onTokenReceived, cancellationToken);

            if (reply.Error is not null)
            {
                return AuditReport.ToolFailure(
                    reportPath,
                    $"# Local Engine Failure\n\nOllama returned an error.\n\n{reply.Error}");
            }

            if (string.IsNullOrWhiteSpace(reply.Content))
            {
                return AuditReport.ToolFailure(
                    reportPath,
                    "# Empty Model Response\n\n" +
                    "Ollama returned no report content. If using a reasoning model, ensure thinking mode is disabled.");
            }

            AuditTokenUsage? usage = reply.PromptTokens is { } promptTokens
                ? new AuditTokenUsage(
                    promptTokens,
                    reply.OutputTokens ?? 0,
                    systemPrompt.Length + userPrompt.Length,
                    _config.NumCtx)
                : null;

            return AuditReport.FromParsedStatus(reportPath, reply.Content, reply.Truncated, usage);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return AuditReport.ToolFailure(
                reportPath,
                $"# Local Engine Failure\n\nUnable to reach Ollama API.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            return AuditReport.ToolFailure(
                reportPath,
                $"# Internal Audit System Error\n\n{ex.Message}");
        }
    }
}
