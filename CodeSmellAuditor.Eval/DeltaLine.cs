namespace CodeSmellAuditor.Eval;

/// <summary>
/// One metric compared between two runs. <paramref name="Spread"/> is the largest min-max range
/// seen across the repeats of either run; a change no larger than that is not a signal.
/// </summary>
public sealed record DeltaLine(
    string Metric,
    double Previous,
    double Current,
    bool HigherIsBetter,
    double? Spread = null)
{
    public double Change => Current - Previous;

    public string Verdict
    {
        get
        {
            if (Math.Abs(Change) < 0.005)
            {
                return "same";
            }

            if (Spread is { } spread && Math.Abs(Change) <= spread)
            {
                return "within spread";
            }

            return (Change > 0) == HigherIsBetter ? "better" : "worse";
        }
    }
}
