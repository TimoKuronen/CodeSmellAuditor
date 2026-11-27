using System.Net;
using System.Text;
using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class OllamaAiOrchestratorTests
{
    private static readonly SourceFile SampleFile = new("Sample.cs", "public class Sample { }");
    private static readonly IReadOnlyList<AuditRule> SampleRules =
        new[] { new AuditRule("rule.mdc", "Prefer interfaces.") };
    private static readonly OllamaChatPrompt SamplePrompt = new("model", "system", "user", 8192, 100, 0);

    [Fact]
    public async Task AnalyzeCodeAsync_StreamsContentAndParsesCompliantStatus()
    {
        string streamBody =
            """{"message":{"content":"Status: "}}""" + "\n" +
            """{"message":{"content":"COMPLIANT\nScore: 90"}}""" + "\n";

        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, streamBody));
        var orchestrator = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), new AuditConfiguration());

        var tokens = new StringBuilder();
        AuditReport report = await orchestrator.AnalyzeCodeAsync(
            SampleFile,
            SampleRules,
            token => tokens.Append(token));

        Assert.Equal(AuditOutcome.Passed, report.Outcome);
        Assert.Contains("COMPLIANT", report.MarkdownCritique);
        Assert.Contains("Status: COMPLIANT", tokens.ToString());
    }

    [Theory]
    [InlineData("length", true, AuditOutcome.ReviewRequired)]
    [InlineData("stop", false, AuditOutcome.Passed)]
    public async Task AnalyzeCodeAsync_ReadsDoneReason(
        string doneReason,
        bool expectedTruncated,
        AuditOutcome expectedOutcome)
    {
        string streamBody =
            """{"message":{"content":"Status: COMPLIANT"},"done":false}""" + "\n" +
            "{\"message\":{\"content\":\"\"},\"done\":true,\"done_reason\":\"" + doneReason + "\"}\n";

        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, streamBody));
        var orchestrator = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), new AuditConfiguration());

        AuditReport report = await orchestrator.AnalyzeCodeAsync(SampleFile, SampleRules, _ => { });

        Assert.Equal(expectedTruncated, report.Truncated);
        Assert.Equal(expectedOutcome, report.Outcome);
    }

    [Fact]
    public async Task AnalyzeCodeAsync_ReadsTokenCountsFromFinalChunk()
    {
        string streamBody =
            """{"message":{"content":"Status: COMPLIANT"},"done":false}""" + "\n" +
            """{"message":{"content":""},"done":true,"done_reason":"stop","prompt_eval_count":1500,"eval_count":40}""" + "\n";

        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, streamBody));
        var config = new AuditConfiguration();
        var orchestrator = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), config);

        AuditReport report = await orchestrator.AnalyzeCodeAsync(SampleFile, SampleRules, _ => { });

        Assert.NotNull(report.Usage);
        Assert.Equal(1500, report.Usage.PromptTokens);
        Assert.Equal(40, report.Usage.OutputTokens);
        Assert.Equal(config.NumCtx, report.Usage.NumCtx);
        Assert.True(report.Usage.PromptChars > 0);
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithoutTokenCounts_LeavesUsageNull()
    {
        string streamBody = """{"message":{"content":"Status: COMPLIANT"}}""" + "\n";
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, streamBody));
        var orchestrator = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), new AuditConfiguration());

        AuditReport report = await orchestrator.AnalyzeCodeAsync(SampleFile, SampleRules, _ => { });

        Assert.Null(report.Usage);
    }

    [Fact]
    public async Task AnalyzeCodeAsync_EmptyBody_ReturnsFailed()
    {
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, "\n"));
        var orchestrator = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), new AuditConfiguration());

        AuditReport report = await orchestrator.AnalyzeCodeAsync(
            SampleFile,
            SampleRules,
            _ => { });

        Assert.Equal(AuditOutcome.Failed, report.Outcome);
        Assert.Contains("Empty Model Response", report.MarkdownCritique);
    }

    [Fact]
    public async Task AnalyzeCodeAsync_HttpFailure_ReturnsFailed()
    {
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.BadGateway, "bad gateway"));
        var orchestrator = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), new AuditConfiguration());

        AuditReport report = await orchestrator.AnalyzeCodeAsync(
            SampleFile,
            SampleRules,
            _ => { });

        Assert.Equal(AuditOutcome.Failed, report.Outcome);
        Assert.Contains("Local Engine Failure", report.MarkdownCritique);
    }

    [Fact]
    public async Task AnalyzeCodeAsync_OllamaErrorLine_ReturnsFailed()
    {
        string streamBody = """{"error":"model not found"}""" + "\n";
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, streamBody));
        var orchestrator = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), new AuditConfiguration());

        AuditReport report = await orchestrator.AnalyzeCodeAsync(
            SampleFile,
            SampleRules,
            _ => { });

        Assert.Equal(AuditOutcome.Failed, report.Outcome);
        Assert.Contains("model not found", report.MarkdownCritique);
    }

    [Fact]
    public async Task AnalyzeCodeAsync_BudgetExceeded_ReturnsFailed()
    {
        var config = new AuditConfiguration(MaxInputCharacters: 10);
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, "{}"));
        var orchestrator = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), config);

        AuditReport report = await orchestrator.AnalyzeCodeAsync(
            SampleFile,
            SampleRules,
            _ => { });

        Assert.Equal(AuditOutcome.Failed, report.Outcome);
        Assert.Contains("Budget Exceeded", report.MarkdownCritique);
    }

    [Fact]
    public async Task AnalyzeCodeAsync_CancelledDuringStream_Throws()
    {
        string streamBody =
            """{"message":{"content":"Status: "}}""" + "\n" +
            """{"message":{"content":"COMPLIANT"}}""" + "\n";
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, streamBody));
        var orchestrator = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), new AuditConfiguration());
        using var cancellation = new CancellationTokenSource();
        var tokens = new List<string>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => orchestrator.AnalyzeCodeAsync(
            SampleFile,
            SampleRules,
            token =>
            {
                tokens.Add(token);
                cancellation.Cancel();
            },
            cancellation.Token));

        Assert.Equal(new[] { "Status: " }, tokens);
    }

    [Fact]
    public async Task CompleteAsync_ReadsSingleResponse()
    {
        string body =
            """{"message":{"content":"done"},"done":true,"done_reason":"length","prompt_eval_count":12,"eval_count":3}""";
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, body));

        OllamaChatReply reply = await new OllamaChatClient(httpClient).CompleteAsync(SamplePrompt);

        Assert.Equal(new OllamaChatReply("done", Truncated: true, PromptTokens: 12, OutputTokens: 3), reply);
    }

    [Fact]
    public async Task CompleteAsync_OllamaError_ReturnsErrorText()
    {
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, """{"error":"model not found"}"""));

        OllamaChatReply reply = await new OllamaChatClient(httpClient).CompleteAsync(SamplePrompt);

        Assert.Equal("model not found", reply.Error);
    }

    [Fact]
    public async Task CompleteAsync_HttpFailure_Throws()
    {
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.BadGateway, "bad gateway"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => new OllamaChatClient(httpClient).CompleteAsync(SamplePrompt));
    }

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri(AuditConfiguration.DefaultOllamaBaseAddress)
        };
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public FakeHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/x-ndjson")
            };
            return Task.FromResult(response);
        }
    }
}
