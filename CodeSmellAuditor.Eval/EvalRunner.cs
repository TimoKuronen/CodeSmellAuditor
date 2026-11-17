using System.Diagnostics;
using System.Net.Http.Json;
using CodeSmellAuditor.Core;

namespace CodeSmellAuditor.Eval;

/// <summary>
/// Audits fixtures through the real engine and measures each call. Rules load once per stack.
/// </summary>
public sealed class EvalRunner
{
    private readonly HttpClient _httpClient;
    private readonly AuditEngine _engine;
    private readonly EvalPaths _paths;
    private readonly string _model;
    private readonly Dictionary<string, IReadOnlyList<AuditRule>> _rulesByStack = new();

    public EvalRunner(HttpClient httpClient, AuditConfiguration config, EvalPaths paths)
    {
        _httpClient = httpClient;
        _paths = paths;
        _model = config.ModelName;
        _engine = new AuditEngine(new MarkdownRuleRepository(), new OllamaAiOrchestrator(httpClient, config));
    }

    /// <summary>
    /// Loads the model into memory so the first timed audit does not include the load.
    /// </summary>
    public async Task WarmUpAsync()
    {
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            "/api/generate",
            new { model = _model, keep_alive = "15m" });

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Ollama could not load model '{_model}' (HTTP {(int)response.StatusCode}). Is it installed?");
        }
    }

    public async Task<FixtureRunResult> RunFixtureAsync(AnswerKey key, string reportsDirectory)
    {
        string fixturePath = Path.Combine(_paths.FixturesDir, key.Fixture);
        Directory.CreateDirectory(reportsDirectory);

        IReadOnlyList<AuditRule> rules = await LoadRulesAsync(key.Stack);
        string source = await File.ReadAllTextAsync(fixturePath);

        var stopwatch = Stopwatch.StartNew();
        FileAuditResult result;
        try
        {
            result = await _engine.AuditFileAsync(
                fixturePath,
                rules,
                reportsDirectory,
                stackName: key.Stack);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            Console.WriteLine($"    audit error: {ex.Message}");
            return Failed(key, stopwatch.Elapsed.TotalSeconds, source);
        }

        stopwatch.Stop();

        if (result.Outcome == AuditOutcome.Failed || string.IsNullOrEmpty(result.ReportPath))
        {
            Console.WriteLine($"    audit failed: {result.FailureReason}");
            return Failed(key, stopwatch.Elapsed.TotalSeconds, source);
        }

        string sidecarPath = AuditSummaryFormatter.GetSummaryPath(result.ReportPath);
        string sidecarJson = await File.ReadAllTextAsync(sidecarPath);
        string reportText = await File.ReadAllTextAsync(result.ReportPath);
        SidecarSummary summary = SidecarSummary.Parse(sidecarJson);

        return new FixtureRunResult(
            FixtureScorer.Score(key, summary, source),
            stopwatch.Elapsed.TotalSeconds,
            summary.Usage?.PromptTokens ?? 0,
            summary.Usage?.OutputTokens ?? 0,
            summary.Usage?.ContextHeadroomTokens ?? 0,
            ClaudeTokenEstimator.Estimate(reportText),
            ClaudeTokenEstimator.Estimate(sidecarJson),
            ClaudeTokenEstimator.Estimate(source));
    }

    private static FixtureRunResult Failed(AnswerKey key, double seconds, string source) =>
        new(FixtureScorer.Score(key, null), seconds, 0, 0, 0, 0, 0, ClaudeTokenEstimator.Estimate(source));

    private async Task<IReadOnlyList<AuditRule>> LoadRulesAsync(string? stack)
    {
        string cacheKey = stack ?? string.Empty;
        if (!_rulesByStack.TryGetValue(cacheKey, out IReadOnlyList<AuditRule>? rules))
        {
            rules = await _engine.LoadRulesAsync(_paths.RulesDir, stack);
            _rulesByStack[cacheKey] = rules;
        }

        return rules;
    }
}
