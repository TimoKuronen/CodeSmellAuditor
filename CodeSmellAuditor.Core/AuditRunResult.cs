namespace CodeSmellAuditor.Core;

public record FileAuditResult(string FileName, string ReportPath, bool HasPassed);

public record AuditRunResult(IReadOnlyList<FileAuditResult> FileResults)
{
    public bool AllPassed => FileResults.Count > 0 && FileResults.All(result => result.HasPassed);
}
