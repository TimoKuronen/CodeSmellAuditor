using System.Text.Json;

namespace CodeSmellAuditor.Eval;

/// <summary>
/// The parts of the audit summary sidecar the evaluation reads.
/// </summary>
public sealed record SidecarSummary(
    string Status,
    bool FindingsTruncated,
    IReadOnlyList<SidecarFinding> Findings,
    SidecarUsage? Usage,
    IReadOnlyList<SidecarPreCheck>? PreChecks)
{
    public const string FailedStatus = "FAILED";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<SidecarPreCheck> PreCheckList => PreChecks ?? Array.Empty<SidecarPreCheck>();

    public static SidecarSummary Parse(string json) =>
        JsonSerializer.Deserialize<SidecarSummary>(json, JsonOptions)
        ?? throw new InvalidDataException("Summary sidecar is empty.");
}

public sealed record SidecarFinding(
    string Id,
    string? File,
    int? Line,
    string? Rule,
    bool Grounded,
    string Issue,
    string? Evidence);

public sealed record SidecarUsage(
    int PromptTokens,
    int OutputTokens,
    int PromptChars,
    int NumCtx,
    double CharsPerToken,
    int ContextHeadroomTokens);

public sealed record SidecarPreCheck(string Id, string Check, int Line, string Message, string? Evidence);
