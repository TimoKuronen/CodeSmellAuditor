using Xunit;
using CodeSmellAuditor.Eval;

namespace CodeSmellAuditor.Core.Tests;

public class FixtureScorerTests
{
    private static AnswerKey Key(string expectedOutcome = "ReviewRequired", params AnswerKeyItem[] items) =>
        new("Sample.cs", null, expectedOutcome, items);

    private static SidecarFinding Finding(string id, int? line, string issue, bool grounded = true, string? evidence = null) =>
        new(id, "Sample.cs", line, null, grounded, issue, evidence);

    private static SidecarSummary Summary(
        string status = "REVIEW REQUIRED",
        IReadOnlyList<SidecarFinding>? findings = null,
        IReadOnlyList<SidecarPreCheck>? preChecks = null,
        bool truncated = false) =>
        new(status, truncated, findings ?? Array.Empty<SidecarFinding>(), null, preChecks);

    private static readonly AnswerKeyItem EmptyCatch =
        new("B1", "blatant", [10, 12], AnyOf: ["(?i)empty catch"]);

    private static readonly AnswerKeyItem NullAfterUse =
        new("J1", "judgment", [20, 22], AnyOf: ["(?i)null"]);

    [Fact]
    public void GroundedFindingOnLineWithMatchingTextIsAHit()
    {
        FixtureScore score = FixtureScorer.Score(
            Key(items: EmptyCatch),
            Summary(findings: [Finding("F1", 11, "Empty catch swallows errors.")]));

        Assert.Equal(1, score.ModelBlatant);
        Assert.Equal(1, score.CombinedBlatant);
        Assert.Equal(0, score.Noise);
        Assert.Equal(["B1"], score.HitIds);
    }

    [Fact]
    public void LineToleranceAcceptsNearbyLinesButNotFarOnes()
    {
        FixtureScore near = FixtureScorer.Score(
            Key(items: EmptyCatch),
            Summary(findings: [Finding("F1", 12 + FixtureScorer.LineTolerance, "empty catch")]));
        FixtureScore far = FixtureScorer.Score(
            Key(items: EmptyCatch),
            Summary(findings: [Finding("F1", 12 + FixtureScorer.LineTolerance + 1, "empty catch")]));

        Assert.Equal(1, near.CombinedBlatant);
        Assert.Equal(0, far.CombinedBlatant);
        Assert.Equal(1, far.Noise);
    }

    [Fact]
    public void RightLineWithUnrelatedTextIsNoiseNotAHit()
    {
        FixtureScore score = FixtureScorer.Score(
            Key(items: EmptyCatch),
            Summary(findings: [Finding("F1", 11, "Method is too long.")]));

        Assert.Equal(0, score.CombinedBlatant);
        Assert.Equal(1, score.Noise);
        Assert.Equal(["B1"], score.MissedIds);
    }

    [Fact]
    public void EvidenceLocationOverridesADriftedReportedLine()
    {
        string source = "a\nb\nc\n    catch (Exception) { }\ne\nf\ng\nh\ni\nj\nk\nl\n";
        AnswerKeyItem item = new("B1", "blatant", [4, 4], AnyOf: ["(?i)empty catch"]);

        FixtureScore score = FixtureScorer.Score(
            Key(items: item),
            Summary(findings: [Finding("F1", 11, "Empty catch.", evidence: "catch (Exception) { }")]),
            source);

        Assert.Equal(1, score.CombinedBlatant);
    }

    [Fact]
    public void ResolveLinePicksOccurrenceNearestReportedLineAndKeepsLineWhenNotFound()
    {
        string[] lines = ["x = 1;", "y", "z", "x = 1;"];
        SidecarFinding near4 = Finding("F1", 5, "i", evidence: "x = 1;");
        SidecarFinding missing = Finding("F2", 7, "i", evidence: "nowhere");
        SidecarFinding none = Finding("F3", 2, "i");

        Assert.Equal(4, FixtureScorer.ResolveLine(near4, lines));
        Assert.Equal(7, FixtureScorer.ResolveLine(missing, lines));
        Assert.Equal(2, FixtureScorer.ResolveLine(none, lines));
    }

    [Fact]
    public void FindingWithoutLineMatchesOnTextAlone()
    {
        FixtureScore score = FixtureScorer.Score(
            Key(items: EmptyCatch),
            Summary(findings: [Finding("F1", null, "Empty catch block.")]));

        Assert.Equal(1, score.CombinedBlatant);
    }

    [Fact]
    public void UngroundedFindingsNeverHitAndAreCountedSeparately()
    {
        FixtureScore score = FixtureScorer.Score(
            Key(items: EmptyCatch),
            Summary(findings: [Finding("F1", 11, "Empty catch.", grounded: false)]));

        Assert.Equal(0, score.CombinedBlatant);
        Assert.Equal(1, score.Ungrounded);
        Assert.Equal(0, score.Noise);
    }

    [Fact]
    public void OneFindingSatisfiesAtMostOneItem()
    {
        AnswerKeyItem second = new("B2", "blatant", [10, 12], AnyOf: ["(?i)empty catch"]);

        FixtureScore score = FixtureScorer.Score(
            Key(items: [EmptyCatch, second]),
            Summary(findings: [Finding("F1", 11, "Empty catch.")]));

        Assert.Equal(1, score.CombinedBlatant);
        Assert.Equal(["B2"], score.MissedIds);
    }

    [Fact]
    public void BlatantAndJudgmentHitsAreCountedByKind()
    {
        FixtureScore score = FixtureScorer.Score(
            Key(items: [EmptyCatch, NullAfterUse]),
            Summary(findings:
            [
                Finding("F1", 11, "Empty catch."),
                Finding("F2", 21, "Null check after the dereference.")
            ]));

        Assert.Equal(1, score.ModelBlatant);
        Assert.Equal(1, score.ModelJudgment);
        Assert.Equal(1, score.BlatantTotal);
        Assert.Equal(1, score.JudgmentTotal);
        Assert.Equal(2, score.CombinedHits);
    }

    [Fact]
    public void PreCheckSatisfiesItemWithoutAModelFinding()
    {
        AnswerKeyItem marker = new("N3", "blatant", [30, 30], Precheck: "wip-marker");

        FixtureScore score = FixtureScorer.Score(
            Key(items: marker),
            Summary(preChecks: [new SidecarPreCheck("P1", "wip-marker", 30, "TODO marker.", null)]));

        Assert.Equal(0, score.ModelHits);
        Assert.Equal(1, score.PreCheckHits);
        Assert.Equal(1, score.CombinedBlatant);
    }

    [Fact]
    public void PreCheckOfTheWrongKindOrLineDoesNotHit()
    {
        AnswerKeyItem marker = new("N3", "blatant", [30, 30], Precheck: "wip-marker");

        FixtureScore wrongCheck = FixtureScorer.Score(
            Key(items: marker),
            Summary(preChecks: [new SidecarPreCheck("P1", "commented-out-code", 30, "x", null)]));
        FixtureScore wrongLine = FixtureScorer.Score(
            Key(items: marker),
            Summary(preChecks: [new SidecarPreCheck("P1", "wip-marker", 90, "x", null)]));

        Assert.Equal(0, wrongCheck.CombinedBlatant);
        Assert.Equal(0, wrongLine.CombinedBlatant);
    }

    [Fact]
    public void CleanFixtureCountsEveryFindingAndPreCheckAsAFalsePositive()
    {
        FixtureScore score = FixtureScorer.Score(
            Key("Passed"),
            Summary(
                status: "REVIEW REQUIRED",
                findings: [Finding("F1", 5, "Something.")],
                preChecks: [new SidecarPreCheck("P1", "wip-marker", 9, "x", null)]));

        Assert.Equal(1, score.Noise);
        Assert.Equal(2, score.CleanFindings);
        Assert.True(score.StatusMismatch);
    }

    [Fact]
    public void CleanFixtureWithCompliantStatusHasNoMismatch()
    {
        FixtureScore score = FixtureScorer.Score(Key("Passed"), Summary(status: "COMPLIANT"));

        Assert.False(score.StatusMismatch);
        Assert.Equal(0, score.CleanFindings);
    }

    [Fact]
    public void MissingOrFailedSummaryScoresAsFailureWithAllItemsMissed()
    {
        FixtureScore missing = FixtureScorer.Score(Key(items: [EmptyCatch, NullAfterUse]), null);
        FixtureScore failed = FixtureScorer.Score(Key(items: EmptyCatch), Summary(status: "FAILED"));

        Assert.True(missing.Failed);
        Assert.Equal(["B1", "J1"], missing.MissedIds);
        Assert.True(failed.Failed);
        Assert.Equal(0, failed.CombinedHits);
    }

    [Fact]
    public void TruncationIsCarriedFromTheSummary()
    {
        FixtureScore score = FixtureScorer.Score(Key(items: EmptyCatch), Summary(truncated: true));

        Assert.True(score.Truncated);
    }

    [Fact]
    public void AnswerKeyLoadsFromJson()
    {
        string path = Path.Combine(Path.GetTempPath(), $"key-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            { "fixture": "A.cs", "stack": "Unity", "expectedOutcome": "Passed",
              "items": [ { "id": "X1", "kind": "judgment", "lines": [3, 5], "anyOf": ["foo"] } ] }
            """);

        try
        {
            AnswerKey key = AnswerKey.Load(path);

            Assert.Equal("A.cs", key.Fixture);
            Assert.Equal("Unity", key.Stack);
            Assert.Equal("COMPLIANT", key.ExpectedStatus);
            Assert.True(key.Items[0].IsJudgment);
            Assert.Equal(3, key.Items[0].FromLine);
            Assert.Equal(5, key.Items[0].ToLine);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
