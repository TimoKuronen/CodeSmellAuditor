namespace CodeSmellAuditor.Core;

public record FileAuditResult(string FileName, string ReportPath, bool HasPassed);

public record AuditRunResult(IReadOnlyList<FileAuditResult> FileResults)
{
    public bool AllPassed => FileResults.Count > 0 && FileResults.All(result => result.HasPassed);

    /// <summary>
    /// Process exit code for scriptable hosts: 0 when every audited file passed, otherwise 1.
    /// Empty runs are treated as failure (nothing to pass).
    /// </summary>
    public int ExitCode => AllPassed ? 0 : 1;
}
