namespace CodeSmellAuditor.Eval;

/// <summary>
/// Collapses the per-fixture results of several repeats into one results row. Counts are summed
/// over the fixtures of a repeat, then averaged over repeats; min and max keep the spread.
/// </summary>
public static class RunAggregator
{
    public static ResultRow Aggregate(RunIdentity identity, IReadOnlyList<IReadOnlyList<FixtureRunResult>> repeats)
    {
        if (repeats.Count == 0 || repeats.Any(repeat => repeat.Count == 0))
        {
            throw new ArgumentException("At least one repeat with at least one fixture is required.", nameof(repeats));
        }

        IReadOnlyList<FixtureRunResult> first = repeats[0];
        List<FixtureRunResult> all = repeats.SelectMany(repeat => repeat).ToList();
        List<FixtureRunResult> withUsage = all.Where(r => r.PromptTokens > 0).ToList();

        double PerRepeatMean(Func<FixtureRunResult, double> selector) =>
            repeats.Average(repeat => repeat.Sum(selector));

        return new ResultRow
        {
            RunId = identity.RunId,
            DateUtc = identity.DateUtc.ToString("yyyy-MM-dd HH:mm"),
            Label = Sanitize(identity.Label),
            Model = identity.Model,
            GitSha = identity.GitSha,
            Dirty = identity.Dirty,
            Repeats = repeats.Count,
            CorpusHash = identity.CorpusHash,
            PromptHash = identity.PromptHash,
            RulesHash = identity.RulesHash,
            CoreHash = identity.CoreHash,
            BlatantTotal = first.Sum(r => r.Score.BlatantTotal),
            JudgmentTotal = first.Sum(r => r.Score.JudgmentTotal),
            ModelBlatant = PerRepeatMean(r => r.Score.ModelBlatant),
            ModelJudgment = PerRepeatMean(r => r.Score.ModelJudgment),
            PreCheckHits = PerRepeatMean(r => r.Score.PreCheckHits),
            CombinedBlatant = PerRepeatMean(r => r.Score.CombinedBlatant),
            CombinedBlatantMin = repeats.Min(repeat => repeat.Sum(r => r.Score.CombinedBlatant)),
            CombinedBlatantMax = repeats.Max(repeat => repeat.Sum(r => r.Score.CombinedBlatant)),
            CombinedJudgment = PerRepeatMean(r => r.Score.CombinedJudgment),
            CombinedJudgmentMin = repeats.Min(repeat => repeat.Sum(r => r.Score.CombinedJudgment)),
            CombinedJudgmentMax = repeats.Max(repeat => repeat.Sum(r => r.Score.CombinedJudgment)),
            Noise = PerRepeatMean(r => r.Score.Noise),
            NoiseMin = repeats.Min(repeat => repeat.Sum(r => r.Score.Noise)),
            NoiseMax = repeats.Max(repeat => repeat.Sum(r => r.Score.Noise)),
            Ungrounded = PerRepeatMean(r => r.Score.Ungrounded),
            CleanFindings = PerRepeatMean(r => r.Score.CleanFindings),
            Truncated = PerRepeatMean(r => r.Score.Truncated ? 1 : 0),
            Failed = PerRepeatMean(r => r.Score.Failed ? 1 : 0),
            StatusMismatches = PerRepeatMean(r => r.Score.StatusMismatch ? 1 : 0),
            SecondsPerFile = all.Average(r => r.Seconds),
            PromptTokens = PerRepeatMean(r => r.PromptTokens),
            OutputTokens = PerRepeatMean(r => r.OutputTokens),
            MinHeadroomTokens = withUsage.Count == 0 ? 0 : withUsage.Min(r => r.HeadroomTokens),
            ClaudeSummarizeTokens = PerRepeatMean(r => r.ReportTokens),
            ClaudeSidecarTokens = PerRepeatMean(r => r.SidecarTokens),
            ClaudeVerifyTokens = PerRepeatMean(r => r.ReportTokens + r.SourceTokens)
        };
    }

    public static string Sanitize(string label) =>
        label.Replace(',', ';').Replace('\r', ' ').Replace('\n', ' ').Trim();
}
