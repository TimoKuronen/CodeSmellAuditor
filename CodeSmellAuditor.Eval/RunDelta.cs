namespace CodeSmellAuditor.Eval;

/// <summary>
/// Comparison of a run against the previous run of the same model. Rows from different corpora
/// are not comparable because their hit totals measure different things.
/// </summary>
public sealed record RunDelta(bool Comparable, IReadOnlyList<string> Changed, IReadOnlyList<DeltaLine> Lines)
{
    public static RunDelta Compute(ResultRow previous, ResultRow current)
    {
        var changed = new List<string>();
        if (previous.CorpusHash != current.CorpusHash) changed.Add("corpus");
        if (previous.PromptHash != current.PromptHash) changed.Add("prompt");
        if (previous.RulesHash != current.RulesHash) changed.Add("rules");
        if (previous.CoreHash != current.CoreHash) changed.Add("core code");

        bool comparable = previous.CorpusHash == current.CorpusHash;

        double BlatantSpread() => Math.Max(
            previous.CombinedBlatantMax - previous.CombinedBlatantMin,
            current.CombinedBlatantMax - current.CombinedBlatantMin);

        double JudgmentSpread() => Math.Max(
            previous.CombinedJudgmentMax - previous.CombinedJudgmentMin,
            current.CombinedJudgmentMax - current.CombinedJudgmentMin);

        double NoiseSpread() => Math.Max(
            previous.NoiseMax - previous.NoiseMin,
            current.NoiseMax - current.NoiseMin);

        var lines = new List<DeltaLine>
        {
            new($"Blatant hits (of {current.BlatantTotal})", previous.CombinedBlatant, current.CombinedBlatant, true, BlatantSpread()),
            new($"Judgment hits (of {current.JudgmentTotal})", previous.CombinedJudgment, current.CombinedJudgment, true, JudgmentSpread()),
            new("Model-only hits", previous.ModelHits, current.ModelHits, true),
            new("Pre-check hits", previous.PreCheckHits, current.PreCheckHits, true),
            new("Noise findings", previous.Noise, current.Noise, false, NoiseSpread()),
            new("Ungrounded findings", previous.Ungrounded, current.Ungrounded, false),
            new("Findings on clean control", previous.CleanFindings, current.CleanFindings, false),
            new("Truncated reports", previous.Truncated, current.Truncated, false),
            new("Failed audits", previous.Failed, current.Failed, false),
            new("Status mismatches", previous.StatusMismatches, current.StatusMismatches, false),
            new("Seconds per file", previous.SecondsPerFile, current.SecondsPerFile, false),
            new("Ollama prompt tokens", previous.PromptTokens, current.PromptTokens, false),
            new("Ollama output tokens", previous.OutputTokens, current.OutputTokens, false),
            new("Claude tokens, summarize (est)", previous.ClaudeSummarizeTokens, current.ClaudeSummarizeTokens, false),
            new("Claude tokens, sidecar only (est)", previous.ClaudeSidecarTokens, current.ClaudeSidecarTokens, false),
            new("Claude tokens, verify (est)", previous.ClaudeVerifyTokens, current.ClaudeVerifyTokens, false)
        };

        return new RunDelta(comparable, changed, lines);
    }
}
