namespace CodeSmellAuditor.Core;

/// <summary>
/// One exact finding from the deterministic checks that run on the source before the model does.
/// </summary>
public record PreCheckFinding(string Id, string Check, int Line, string Message, string? Evidence);
