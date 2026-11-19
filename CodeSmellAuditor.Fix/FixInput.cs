namespace CodeSmellAuditor.Fix;

/// <summary>
/// One C# file handed to a fixer.
/// </summary>
public sealed record FixInput(string Path, string Content);
