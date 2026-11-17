namespace CodeSmellAuditor.Eval;

/// <summary>
/// Scoring of one fixture audit against its answer key. "Model" counts items hit by a grounded
/// model finding, "PreCheck" items hit by a deterministic check, "Combined" items hit by either.
/// </summary>
public sealed record FixtureScore(
    string Fixture,
    int BlatantTotal,
    int JudgmentTotal,
    int ModelBlatant,
    int ModelJudgment,
    int PreCheckHits,
    int CombinedBlatant,
    int CombinedJudgment,
    int Noise,
    int Ungrounded,
    int CleanFindings,
    bool Truncated,
    bool Failed,
    bool StatusMismatch,
    IReadOnlyList<string> HitIds,
    IReadOnlyList<string> MissedIds,
    IReadOnlyList<string> NoiseFindings)
{
    public int CombinedHits => CombinedBlatant + CombinedJudgment;

    public int ModelHits => ModelBlatant + ModelJudgment;

    public int ItemTotal => BlatantTotal + JudgmentTotal;
}
