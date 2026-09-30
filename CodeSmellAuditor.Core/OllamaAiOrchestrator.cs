using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeSmellAuditor.Core;

public class OllamaAiOrchestrator : IAiOrchestrator
{
    private readonly HttpClient _httpClient;
    private readonly AuditConfiguration _config;

    public OllamaAiOrchestrator(HttpClient httpClient, AuditConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<AuditReport> AnalyzeCodeAsync(
        SourceFile file,
        IEnumerable<AuditRule> rules,
        Action<string> onTokenReceived)
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

        return await CompleteChatAsync(file.FilePath, systemPrompt, userPrompt, onTokenReceived);
    }

    public async Task<AuditReport> AnalyzeSystemAsync(
        IReadOnlyList<SourceFile> files,
        string? manifestText,
        IEnumerable<AuditRule> rules,
        Action<string> onTokenReceived)
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
            onTokenReceived);
    }

    private async Task<AuditReport> CompleteChatAsync(
        string reportPath,
        string systemPrompt,
        string userPrompt,
        Action<string> onTokenReceived)
    {
        var requestPayload = new OllamaChatRequest
        {
            Model = _config.ModelName,
            Stream = true,
            Think = false,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            },
            Options = new Dictionary<string, object>
            {
                { "num_ctx", _config.NumCtx },
                { "num_predict", _config.NumPredict },
                { "temperature", 0.2 }
            }
        };

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string serializedBody = JsonSerializer.Serialize(requestPayload, jsonOptions);
            using var contentStream = new StringContent(serializedBody, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = contentStream
            };

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var networkStream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(networkStream);

            var completeReportBuilder = new StringBuilder();

            while (await reader.ReadLineAsync() is { } jsonLine)
            {
                if (string.IsNullOrWhiteSpace(jsonLine))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(jsonLine);

                if (doc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    string errorText = errorElement.ValueKind == JsonValueKind.String
                        ? errorElement.GetString() ?? "Unknown Ollama error"
                        : errorElement.ToString();

                    return AuditReport.ToolFailure(
                        reportPath,
                        $"# Local Engine Failure\n\nOllama returned an error.\n\n{errorText}");
                }

                if (doc.RootElement.TryGetProperty("message", out var messageElement) &&
                    messageElement.TryGetProperty("content", out var contentElement))
                {
                    string token = contentElement.GetString() ?? string.Empty;

                    if (!string.IsNullOrEmpty(token))
                    {
                        completeReportBuilder.Append(token);
                        onTokenReceived(token);
                    }
                }
            }

            string fullTextResult = completeReportBuilder.ToString();
            if (string.IsNullOrWhiteSpace(fullTextResult))
            {
                return AuditReport.ToolFailure(
                    reportPath,
                    "# Empty Model Response\n\n" +
                    "Ollama returned no report content. If using a reasoning model, ensure thinking mode is disabled.");
            }

            return AuditReport.FromParsedStatus(reportPath, fullTextResult);
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
