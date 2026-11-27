namespace CodeSmellAuditor.Eval;

/// <summary>
/// One row of the results table: a model's averaged metrics over all fixtures and repeats.
/// Token and hit fields are per full corpus pass.
/// </summary>
public sealed class ResultRow
{
    public string RunId { get; set; } = string.Empty;
    public string DateUtc { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string GitSha { get; set; } = string.Empty;
    public bool Dirty { get; set; }
    public int Repeats { get; set; }
    public string CorpusHash { get; set; } = string.Empty;
    public string PromptHash { get; set; } = string.Empty;
    public string RulesHash { get; set; } = string.Empty;
    public string CoreHash { get; set; } = string.Empty;

    public int BlatantTotal { get; set; }
    public int JudgmentTotal { get; set; }
    public double ModelBlatant { get; set; }
    public double ModelJudgment { get; set; }
    public double PreCheckHits { get; set; }
    public double CombinedBlatant { get; set; }
    public double CombinedBlatantMin { get; set; }
    public double CombinedBlatantMax { get; set; }
    public double CombinedJudgment { get; set; }
    public double CombinedJudgmentMin { get; set; }
    public double CombinedJudgmentMax { get; set; }

    public double Noise { get; set; }
    public double NoiseMin { get; set; }
    public double NoiseMax { get; set; }
    public double Ungrounded { get; set; }
    public double CleanFindings { get; set; }
    public double Truncated { get; set; }
    public double Failed { get; set; }
    public double StatusMismatches { get; set; }

    public double SecondsPerFile { get; set; }
    public double PromptTokens { get; set; }
    public double OutputTokens { get; set; }
    public int MinHeadroomTokens { get; set; }

    public double ReadReportTokens { get; set; }
    public double ReadSidecarTokens { get; set; }
    public double ReadVerifyTokens { get; set; }

    public double ModelHits => ModelBlatant + ModelJudgment;
}
