using System.Text.RegularExpressions;

namespace CodeSmellAuditor.Eval;

/// <summary>
/// Scores a fixture's summary sidecar against its answer key. Each grounded model finding can
/// satisfy at most one item; grounded findings that satisfy none are noise.
/// </summary>
public static class FixtureScorer
{
    public const int LineTolerance = 2;

    /// <summary>
    /// Scores one audit. When <paramref name="sourceText"/> is given, each finding's line is
    /// re-derived from where its evidence appears in the source, because the line the model
    /// reports drifts and the sidecar falls back to it when the evidence spans lines.
    /// </summary>
    public static FixtureScore Score(AnswerKey key, SidecarSummary? summary, string? sourceText = null)
    {
        int blatantTotal = key.Items.Count(item => !item.IsJudgment);
        int judgmentTotal = key.Items.Count(item => item.IsJudgment);

        if (summary is null || summary.Status == SidecarSummary.FailedStatus)
        {
            return new FixtureScore(
                key.Fixture, blatantTotal, judgmentTotal, 0, 0, 0, 0, 0, 0, 0, 0,
                Truncated: false, Failed: true, StatusMismatch: true,
                HitIds: Array.Empty<string>(),
                MissedIds: key.Items.Select(item => item.Id).ToList(),
                NoiseFindings: Array.Empty<string>());
        }

        string[] sourceLines = sourceText?.Split('\n') ?? Array.Empty<string>();
        List<SidecarFinding> grounded = summary.Findings
            .Where(f => f.Grounded)
            .Select(f => f with { Line = ResolveLine(f, sourceLines) })
            .ToList();
        var used = new bool[grounded.Count];

        int modelBlatant = 0, modelJudgment = 0, preCheckHits = 0, combinedBlatant = 0, combinedJudgment = 0;
        var hitIds = new List<string>();
        var missedIds = new List<string>();

        foreach (AnswerKeyItem item in key.Items)
        {
            bool modelHit = false;
            for (int i = 0; i < grounded.Count; i++)
            {
                if (!used[i] && MatchesFinding(item, grounded[i]))
                {
                    used[i] = true;
                    modelHit = true;
                    break;
                }
            }

            bool preCheckHit = item.Precheck is not null
                               && summary.PreCheckList.Any(p => MatchesPreCheck(item, p));

            if (modelHit)
            {
                if (item.IsJudgment) modelJudgment++; else modelBlatant++;
            }

            if (preCheckHit)
            {
                preCheckHits++;
            }

            if (modelHit || preCheckHit)
            {
                if (item.IsJudgment) combinedJudgment++; else combinedBlatant++;
                hitIds.Add(item.Id);
            }
            else
            {
                missedIds.Add(item.Id);
            }
        }

        List<string> noiseFindings = grounded
            .Where((_, index) => !used[index])
            .Select(f => $"{f.Id} line {f.Line}: {f.Issue}")
            .ToList();

        bool cleanFixture = key.Items.Count == 0;

        return new FixtureScore(
            key.Fixture,
            blatantTotal,
            judgmentTotal,
            modelBlatant,
            modelJudgment,
            preCheckHits,
            combinedBlatant,
            combinedJudgment,
            noiseFindings.Count,
            summary.Findings.Count(f => !f.Grounded),
            cleanFixture ? noiseFindings.Count + summary.PreCheckList.Count : 0,
            summary.FindingsTruncated,
            Failed: false,
            StatusMismatch: !string.Equals(summary.Status, key.ExpectedStatus, StringComparison.Ordinal),
            hitIds,
            missedIds,
            noiseFindings);
    }

    /// <summary>
    /// Line where the finding's evidence sits in the source (the occurrence nearest the reported
    /// line); the reported line when the evidence is empty or not found line-by-line.
    /// </summary>
    public static int? ResolveLine(SidecarFinding finding, IReadOnlyList<string> sourceLines)
    {
        string evidence = finding.Evidence?.Split('\n')[0].Trim() ?? string.Empty;
        if (evidence.Length == 0 || sourceLines.Count == 0)
        {
            return finding.Line;
        }

        int? best = null;
        for (int i = 0; i < sourceLines.Count; i++)
        {
            if (!sourceLines[i].Contains(evidence, StringComparison.Ordinal))
            {
                continue;
            }

            int candidate = i + 1;
            if (best is null
                || Math.Abs(candidate - (finding.Line ?? candidate)) < Math.Abs(best.Value - (finding.Line ?? candidate)))
            {
                best = candidate;
            }
        }

        return best ?? finding.Line;
    }

    public static bool MatchesFinding(AnswerKeyItem item, SidecarFinding finding)
    {
        if (finding.Line is { } line && !LineInRange(item, line))
        {
            return false;
        }

        if (item.AnyOf is not { Length: > 0 })
        {
            return true;
        }

        string text = $"{finding.Evidence}\n{finding.Issue}";
        return item.AnyOf.Any(pattern => Regex.IsMatch(text, pattern));
    }

    public static bool MatchesPreCheck(AnswerKeyItem item, SidecarPreCheck preCheck) =>
        string.Equals(item.Precheck, preCheck.Check, StringComparison.Ordinal)
        && LineInRange(item, preCheck.Line);

    private static bool LineInRange(AnswerKeyItem item, int line) =>
        line >= item.FromLine - LineTolerance && line <= item.ToLine + LineTolerance;
}
