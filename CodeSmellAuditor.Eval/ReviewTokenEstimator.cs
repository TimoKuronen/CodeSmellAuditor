namespace CodeSmellAuditor.Eval;

/// <summary>
/// Offline estimate of the tokens a downstream reviewer spends reading a text. Code and markdown average
/// about 3.5 characters per token; the figure is for comparing runs, not for billing.
/// </summary>
public static class ReviewTokenEstimator
{
    public const double CharsPerToken = 3.5;

    public static int Estimate(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / CharsPerToken);
}
