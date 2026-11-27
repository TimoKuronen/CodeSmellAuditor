using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Calls Ollama <c>/api/chat</c> with thinking disabled. The client's base address names the server.
/// HTTP, parsing and cancellation failures surface as exceptions.
/// </summary>
public sealed class OllamaChatClient
{
    private const string OutputLimitDoneReason = "length";

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;

    public OllamaChatClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Streams the reply, passing each piece of content to <paramref name="onTokenReceived"/>.
    /// </summary>
    public async Task<OllamaChatReply> StreamAsync(
        OllamaChatPrompt prompt,
        Action<string> onTokenReceived,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(prompt, stream: true, cancellationToken);
        using Stream networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(networkStream);

        var content = new StringBuilder();
        var reply = new OllamaChatReply(string.Empty);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? jsonLine = await reader.ReadLineAsync(cancellationToken);
            if (jsonLine is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(jsonLine))
            {
                continue;
            }

            using var document = JsonDocument.Parse(jsonLine);
            reply = Merge(reply, document.RootElement);
            if (reply.Error is not null)
            {
                return reply;
            }

            string token = ReadContent(document.RootElement);
            if (token.Length > 0)
            {
                content.Append(token);
                onTokenReceived(token);
            }
        }

        return reply with { Content = content.ToString() };
    }

    /// <summary>
    /// Requests the whole reply as a single response.
    /// </summary>
    public async Task<OllamaChatReply> CompleteAsync(
        OllamaChatPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(prompt, stream: false, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(body);
        OllamaChatReply reply = Merge(new OllamaChatReply(string.Empty), document.RootElement);

        return reply.Error is not null
            ? reply
            : reply with { Content = ReadContent(document.RootElement) };
    }

    private async Task<HttpResponseMessage> SendAsync(
        OllamaChatPrompt prompt,
        bool stream,
        CancellationToken cancellationToken)
    {
        var payload = new OllamaChatRequest
        {
            Model = prompt.Model,
            Stream = stream,
            Think = false,
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = prompt.SystemPrompt },
                new() { Role = "user", Content = prompt.UserPrompt }
            },
            Options = new Dictionary<string, object>
            {
                { "num_ctx", prompt.NumCtx },
                { "num_predict", prompt.NumPredict },
                { "temperature", prompt.Temperature }
            }
        };

        string serializedBody = JsonSerializer.Serialize(payload, RequestJsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(serializedBody, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        return response;
    }

    private static OllamaChatReply Merge(OllamaChatReply reply, JsonElement element)
    {
        if (element.TryGetProperty("error", out JsonElement errorElement))
        {
            string errorText = errorElement.ValueKind == JsonValueKind.String
                ? errorElement.GetString() ?? "Unknown Ollama error"
                : errorElement.ToString();

            return reply with { Error = errorText };
        }

        if (element.TryGetProperty("done_reason", out JsonElement doneReason) &&
            doneReason.ValueKind == JsonValueKind.String &&
            doneReason.GetString() == OutputLimitDoneReason)
        {
            reply = reply with { Truncated = true };
        }

        if (element.TryGetProperty("prompt_eval_count", out JsonElement promptCount) &&
            promptCount.TryGetInt32(out int promptTokens))
        {
            reply = reply with { PromptTokens = promptTokens };
        }

        if (element.TryGetProperty("eval_count", out JsonElement outputCount) &&
            outputCount.TryGetInt32(out int outputTokens))
        {
            reply = reply with { OutputTokens = outputTokens };
        }

        return reply;
    }

    private static string ReadContent(JsonElement element) =>
        element.TryGetProperty("message", out JsonElement message) &&
        message.TryGetProperty("content", out JsonElement content)
            ? content.GetString() ?? string.Empty
            : string.Empty;
}
