namespace CodeSmellAuditor.Core;

public sealed class NullAuditProgressReporter : IAuditProgressReporter
{
    public void RulesLoaded(IReadOnlyList<AuditRule> rules)
    {
    }

    public void NoTargetsFound()
    {
    }

    public Task<AuditReport> RunFileAuditAsync(
        string fileName,
        Func<Action<string>, Task<AuditReport>> analyzeAsync)
    {
        return analyzeAsync(_ => { });
    }

    public void ReportSaved(string fileName, string reportPath)
    {
    }
}
