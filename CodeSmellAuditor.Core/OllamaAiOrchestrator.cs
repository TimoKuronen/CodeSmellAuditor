using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeSmellAuditor.Core;

public class OllamaAiOrchestrator : IAiOrchestrator
{
    private readonly HttpClient _httpClient;
    private readonly string _modelName;

    public OllamaAiOrchestrator(string modelName)
    {
        _httpClient = new HttpClient 
        { 
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromMinutes(10) 
        };
        _modelName = modelName;
    }

    public async Task<AuditReport> AnalyzeCodeAsync(SourceFile file, IEnumerable<AuditRule> rules, Action<string> onTokenReceived)
    {
        var systemInstructions = new StringBuilder();
        systemInstructions.AppendLine("You are an elite automated system architect running static analysis audits on source code scripts.");
        systemInstructions.AppendLine("Your goal is to enforce the following strict architectural governance rules:");
        
        foreach (var rule in rules)
        {
            systemInstructions.AppendLine($"\n[DOCUMENT: {rule.Name}]");
            systemInstructions.AppendLine(rule.PromptGuideline);
        }
        
        systemInstructions.AppendLine("\nCRITICAL INSTRUCTION: Go directly to outputting the markdown report. Keep internal thinking concise. Output a clear, markdown-formatted report containing a Status metric (COMPLIANT or REVIEW REQUIRED), an alignment score percentage, and analytical critique notes.");

        var userPrompt = $"Target File Path: {file.FilePath}\n\nSource Code Body:\n```csharp\n{file.Content}\n```";

        var requestPayload = new OllamaChatRequest
        {
            Model = _modelName,
            Stream = true,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = systemInstructions.ToString() },
                new() { Role = "user", Content = userPrompt }
            },
            Options = new Dictionary<string, object>
            {
                { "num_ctx", 8192 },
                { "num_predict", 4096 }, 
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

            // Fixed: Construct an explicit HttpRequestMessage to properly use ResponseHeadersRead streaming
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
                if (string.IsNullOrWhiteSpace(jsonLine)) continue;

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
            bool hasPassed = !fullTextResult.Contains("REVIEW REQUIRED", StringComparison.OrdinalIgnoreCase);

            return new AuditReport(file.FilePath, fullTextResult, hasPassed);
        }
        catch (HttpRequestException ex)
        {
            return new AuditReport(file.FilePath, $"# Local Engine Failure\n\nUnable to reach Ollama API.\n\n{ex.Message}", false);
        }
        catch (Exception ex)
        {
            return new AuditReport(file.FilePath, $"# Internal Audit System Error\n\n{ex.Message}", false);
        }
    }
}