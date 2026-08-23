namespace CodeSmellAuditor.Core;

public interface IRuleRepository
{
    Task<IEnumerable<AuditRule>> GetActiveRulesAsync(string rulesFolderPath);
}

public interface IAiOrchestrator
{
    Task<AuditReport> AnalyzeCodeAsync(
        SourceFile file,
        IEnumerable<AuditRule> rules,
        Action<string> onTokenReceived);
}