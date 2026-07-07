using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeSmellAuditor.Core;

public class OllamaAiOrchestrator : IAiOrchestrator
{
    private readonly HttpClient _httpClient;
    private readonly AuditConfiguration _config;

    public OllamaAiOrchestrator(AuditConfiguration config)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromMinutes(10)
        };
        _config = config;
    }

    public OllamaAiOrchestrator(string modelName)
        : this(new AuditConfiguration(ModelName: modelName))
    {
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
            return new AuditReport(file.FilePath, $"# Audit Budget Exceeded\n\n{ex.Message}", false);
        }

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

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
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
                return new AuditReport(
                    file.FilePath,
                    "# Empty Model Response\n\n" +
                    "Ollama returned no report content. If using a reasoning model, ensure thinking mode is disabled.",
                    false);
            }

            bool hasPassed = AuditReportParser.ParsePassStatus(fullTextResult);

            return new AuditReport(file.FilePath, fullTextResult, hasPassed);
        }
        catch (HttpRequestException ex)
        {
            return new AuditReport(
                file.FilePath,
                $"# Local Engine Failure\n\nUnable to reach Ollama API.\n\n{ex.Message}",
                false);
        }
        catch (Exception ex)
        {
            return new AuditReport(file.FilePath, $"# Internal Audit System Error\n\n{ex.Message}", false);
        }
    }
}
