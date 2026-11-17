using Xunit;
using CodeSmellAuditor.Eval;

namespace CodeSmellAuditor.Core.Tests;

public class EvalResultsTests
{
    private static FixtureScore Score(int combinedBlatant, int combinedJudgment, int noise, bool failed = false) =>
        new("S.cs", 4, 2, combinedBlatant, combinedJudgment, 0, combinedBlatant, combinedJudgment,
            noise, 1, 0, false, failed, false, [], [], []);

    private static FixtureRunResult Run(FixtureScore score, double seconds = 10) =>
        new(score, seconds, 1000, 200, 5000, 700, 350, 400);

    private static RunIdentity Identity(string runId = "run-1", string prompt = "p1", string corpus = "c1") =>
        new(runId, new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc), "a, label", "qwen3.5:9b",
            "abc1234", false, corpus, prompt, "r1", "k1");

    [Fact]
    public void AggregateAveragesRepeatsAndKeepsSpread()
    {
        IReadOnlyList<IReadOnlyList<FixtureRunResult>> repeats =
        [
            [Run(Score(2, 0, 3)), Run(Score(1, 1, 1))],
            [Run(Score(4, 0, 1)), Run(Score(1, 1, 1))]
        ];

        ResultRow row = RunAggregator.Aggregate(Identity(), repeats);

        Assert.Equal(2, row.Repeats);
        Assert.Equal(4.0, row.CombinedBlatant);
        Assert.Equal(3.0, row.CombinedBlatantMin);
        Assert.Equal(5.0, row.CombinedBlatantMax);
        Assert.Equal(1.0, row.CombinedJudgment);
        Assert.Equal(3.0, row.Noise);
        Assert.Equal(2.0, row.NoiseMin);
        Assert.Equal(4.0, row.NoiseMax);
        Assert.Equal(8, row.BlatantTotal);
        Assert.Equal(4, row.JudgmentTotal);
    }

    [Fact]
    public void AggregateSumsTokensPerPassAndComputesClaudeModes()
    {
        IReadOnlyList<IReadOnlyList<FixtureRunResult>> repeats = [[Run(Score(1, 0, 0)), Run(Score(1, 0, 0))]];

        ResultRow row = RunAggregator.Aggregate(Identity(), repeats);

        Assert.Equal(2000, row.PromptTokens);
        Assert.Equal(400, row.OutputTokens);
        Assert.Equal(1400, row.ClaudeSummarizeTokens);
        Assert.Equal(700, row.ClaudeSidecarTokens);
        Assert.Equal(2200, row.ClaudeVerifyTokens);
        Assert.Equal(5000, row.MinHeadroomTokens);
    }

    [Fact]
    public void AggregateCountsFailuresAndSanitizesLabel()
    {
        IReadOnlyList<IReadOnlyList<FixtureRunResult>> repeats = [[Run(Score(0, 0, 0, failed: true)), Run(Score(1, 0, 0))]];

        ResultRow row = RunAggregator.Aggregate(Identity(), repeats);

        Assert.Equal(1.0, row.Failed);
        Assert.Equal("a; label", row.Label);
    }

    [Fact]
    public void AggregateRejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => RunAggregator.Aggregate(Identity(), []));
    }

    [Fact]
    public void CsvRoundTripsARow()
    {
        ResultRow row = RunAggregator.Aggregate(Identity(), [[Run(Score(2, 1, 3)), Run(Score(1, 0, 0))]]);

        ResultRow parsed = ResultsCsv.ParseLine(ResultsCsv.ToLine(row));

        Assert.Equal(ResultsCsv.ToLine(row), ResultsCsv.ToLine(parsed));
        Assert.Equal(row.RunId, parsed.RunId);
        Assert.Equal(row.CombinedBlatant, parsed.CombinedBlatant);
        Assert.Equal(row.ClaudeVerifyTokens, parsed.ClaudeVerifyTokens);
    }

    [Fact]
    public void CsvAppendWritesHeaderOnceAndLoadReadsRowsInOrder()
    {
        string path = Path.Combine(Path.GetTempPath(), $"results-{Guid.NewGuid():N}.csv");
        try
        {
            ResultRow first = RunAggregator.Aggregate(Identity("run-1"), [[Run(Score(1, 0, 0))]]);
            ResultRow second = RunAggregator.Aggregate(Identity("run-2"), [[Run(Score(2, 0, 0))]]);

            ResultsCsv.Append(path, first);
            ResultsCsv.Append(path, second);

            Assert.Equal(3, File.ReadAllLines(path).Length);
            Assert.Equal(["run-1", "run-2"], ResultsCsv.Load(path).Select(r => r.RunId));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CsvLoadOfMissingFileIsEmpty()
    {
        Assert.Empty(ResultsCsv.Load(Path.Combine(Path.GetTempPath(), $"none-{Guid.NewGuid():N}.csv")));
    }

    [Fact]
    public void CsvParseRejectsWrongColumnCount()
    {
        Assert.Throws<FormatException>(() => ResultsCsv.ParseLine("a,b,c"));
    }

    [Fact]
    public void DeltaFlagsChangedInputsAndJudgesDirection()
    {
        ResultRow previous = RunAggregator.Aggregate(Identity("run-1"), [[Run(Score(2, 0, 4))], [Run(Score(2, 0, 4))]]);
        ResultRow current = RunAggregator.Aggregate(Identity("run-2", prompt: "p2"), [[Run(Score(4, 1, 1))], [Run(Score(4, 1, 1))]]);

        RunDelta delta = RunDelta.Compute(previous, current);

        Assert.True(delta.Comparable);
        Assert.Equal(["prompt"], delta.Changed);
        Assert.Equal("better", delta.Lines.Single(l => l.Metric.StartsWith("Blatant")).Verdict);
        Assert.Equal("better", delta.Lines.Single(l => l.Metric.StartsWith("Judgment")).Verdict);
        Assert.Equal("better", delta.Lines.Single(l => l.Metric == "Noise findings").Verdict);
    }

    [Fact]
    public void DeltaTreatsChangeInsideRepeatSpreadAsNoise()
    {
        ResultRow previous = RunAggregator.Aggregate(Identity("run-1"), [[Run(Score(2, 0, 0))], [Run(Score(4, 0, 0))]]);
        ResultRow current = RunAggregator.Aggregate(Identity("run-2"), [[Run(Score(3, 0, 0))], [Run(Score(4, 0, 0))]]);

        RunDelta delta = RunDelta.Compute(previous, current);

        Assert.Equal("within spread", delta.Lines.Single(l => l.Metric.StartsWith("Blatant")).Verdict);
    }

    [Fact]
    public void DeltaMarksWorseWhenNoiseRisesOutsideSpread()
    {
        ResultRow previous = RunAggregator.Aggregate(Identity("run-1"), [[Run(Score(2, 0, 1))]]);
        ResultRow current = RunAggregator.Aggregate(Identity("run-2"), [[Run(Score(2, 0, 5))]]);

        RunDelta delta = RunDelta.Compute(previous, current);

        Assert.Equal("worse", delta.Lines.Single(l => l.Metric == "Noise findings").Verdict);
    }

    [Fact]
    public void DeltaIsNotComparableAcrossCorpusChange()
    {
        ResultRow previous = RunAggregator.Aggregate(Identity("run-1"), [[Run(Score(2, 0, 1))]]);
        ResultRow current = RunAggregator.Aggregate(Identity("run-2", corpus: "c2"), [[Run(Score(2, 0, 1))]]);

        RunDelta delta = RunDelta.Compute(previous, current);

        Assert.False(delta.Comparable);
        Assert.Contains("corpus", delta.Changed);
        Assert.Contains("not comparable", ResultsMarkdown.RenderDelta(previous, current));
    }

    [Fact]
    public void MarkdownShowsFirstRowMessageAndComparesPerModel()
    {
        ResultRow nine1 = RunAggregator.Aggregate(Identity("nine-1"), [[Run(Score(2, 0, 1))]]);
        ResultRow four = RunAggregator.Aggregate(Identity("four-1") with { Model = "qwen3.5:4b" }, [[Run(Score(1, 0, 1))]]);
        ResultRow nine2 = RunAggregator.Aggregate(Identity("nine-2"), [[Run(Score(3, 0, 1))]]);

        string markdown = ResultsMarkdown.Render([nine1, four, nine2]);

        Assert.Contains("Compared with nine-1", markdown);
        Assert.Contains("First row for this model", markdown);
        Assert.True(markdown.IndexOf("| nine-2", StringComparison.Ordinal) < markdown.IndexOf("| nine-1", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("1234567", 2)]
    [InlineData("12345678", 3)]
    public void ClaudeTokenEstimateRoundsUpAtThreePointFiveCharsPerToken(string text, int expected)
    {
        Assert.Equal(expected, ClaudeTokenEstimator.Estimate(text));
    }

    [Fact]
    public void OptionsDefaultToPinnedModelAndThreeRepeats()
    {
        EvalOptions options = EvalOptions.Parse([]);

        Assert.Equal(EvalOptions.PinnedModel, options.Model);
        Assert.Equal(3, options.Repeats);
        Assert.False(options.IsPartial);
    }

    [Fact]
    public void OptionsParseOverridesAndRejectBadInput()
    {
        EvalOptions options = EvalOptions.Parse(["--model", "qwen3.5:4b", "--repeats", "1", "--label", "x", "--only", "Order"]);

        Assert.Equal("qwen3.5:4b", options.Model);
        Assert.Equal(1, options.Repeats);
        Assert.Equal("x", options.Label);
        Assert.True(options.IsPartial);
        Assert.Throws<ArgumentException>(() => EvalOptions.Parse(["--repeats", "0"]));
        Assert.Throws<ArgumentException>(() => EvalOptions.Parse(["--nope"]));
        Assert.Throws<ArgumentException>(() => EvalOptions.Parse(["--model"]));
    }
}
