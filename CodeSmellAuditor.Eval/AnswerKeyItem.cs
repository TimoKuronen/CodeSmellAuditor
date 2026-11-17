namespace CodeSmellAuditor.Eval;

/// <summary>
/// One planted issue in a fixture. A finding hits the item when its line is within the item's
/// line range (plus tolerance) and its text matches one of <paramref name="AnyOf"/>.
/// <paramref name="Precheck"/> names the deterministic check that can also satisfy the item.
/// </summary>
public sealed record AnswerKeyItem(
    string Id,
    string Kind,
    int[] Lines,
    string? Precheck = null,
    string[]? AnyOf = null)
{
    public const string BlatantKind = "blatant";
    public const string JudgmentKind = "judgment";

    public int FromLine => Lines[0];

    public int ToLine => Lines[^1];

    public bool IsJudgment => string.Equals(Kind, JudgmentKind, StringComparison.OrdinalIgnoreCase);
}
