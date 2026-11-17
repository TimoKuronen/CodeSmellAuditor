using System.Text.Json;

namespace CodeSmellAuditor.Eval;

/// <summary>
/// Answer key for one fixture, written from the source before any model run.
/// </summary>
public sealed record AnswerKey(
    string Fixture,
    string? Stack,
    string ExpectedOutcome,
    IReadOnlyList<AnswerKeyItem> Items)
{
    public const string PassedOutcome = "Passed";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string ExpectedStatus => string.Equals(ExpectedOutcome, PassedOutcome, StringComparison.OrdinalIgnoreCase)
        ? "COMPLIANT"
        : "REVIEW REQUIRED";

    public static AnswerKey Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AnswerKey>(json, JsonOptions)
               ?? throw new InvalidDataException($"Answer key is empty: {path}");
    }
}
