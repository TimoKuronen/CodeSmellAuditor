namespace CodeSmellAuditor.Core;

public interface IAuditProgressReporter
{
    void RulesLoaded(IReadOnlyList<AuditRule> rules);

    void NoTargetsFound();

    Task<AuditReport> RunFileAuditAsync(
        string fileName,
        Func<Action<string>, Task<AuditReport>> analyzeAsync);

    void ReportSaved(string fileName, string reportPath);
}
