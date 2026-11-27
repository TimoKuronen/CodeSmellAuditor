namespace CodeSmellAuditor.Eval;

/// <summary>
/// One fixture audit: its score plus the time, Ollama token and reading-cost measurements.
/// </summary>
public sealed record FixtureRunResult(
    FixtureScore Score,
    double Seconds,
    int PromptTokens,
    int OutputTokens,
    int HeadroomTokens,
    int ReportTokens,
    int SidecarTokens,
    int SourceTokens);
