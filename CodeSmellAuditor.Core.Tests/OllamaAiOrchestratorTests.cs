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

    [Fact]
    public async Task AnalyzeCodeAsync_StreamsContentAndParsesCompliantStatus()
    {
        string streamBody =
            """{"message":{"content":"Status: "}}""" + "\n" +
            """{"message":{"content":"COMPLIANT\nScore: 90"}}""" + "\n";

        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, streamBody));
        var orchestrator = new OllamaAiOrchestrator(httpClient, new AuditConfiguration());

        var tokens = new StringBuilder();
        AuditReport report = await orchestrator.AnalyzeCodeAsync(
            SampleFile,
            SampleRules,
            token => tokens.Append(token));

        Assert.Equal(AuditOutcome.Passed, report.Outcome);
        Assert.Contains("COMPLIANT", report.MarkdownCritique);
        Assert.Contains("Status: COMPLIANT", tokens.ToString());
    }

    [Fact]
    public async Task AnalyzeCodeAsync_EmptyBody_ReturnsFailed()
    {
        using var httpClient = CreateClient(new FakeHandler(HttpStatusCode.OK, "\n"));
        var orchestrator = new OllamaAiOrchestrator(httpClient, new AuditConfiguration());

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
        var orchestrator = new OllamaAiOrchestrator(httpClient, new AuditConfiguration());

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
        var orchestrator = new OllamaAiOrchestrator(httpClient, new AuditConfiguration());

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
        var orchestrator = new OllamaAiOrchestrator(httpClient, config);

        AuditReport report = await orchestrator.AnalyzeCodeAsync(
            SampleFile,
            SampleRules,
            _ => { });

        Assert.Equal(AuditOutcome.Failed, report.Outcome);
        Assert.Contains("Budget Exceeded", report.MarkdownCritique);
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
