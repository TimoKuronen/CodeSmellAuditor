namespace CodeSmellAuditor.Core;

public interface IRuleRepository
{
    Task<IEnumerable<AuditRule>> GetActiveRulesAsync(string rulesFolderPath);
}

public interface IAiOrchestrator
{
// Added Action<string> onTokenReceived to pump real-time text to the presentation layer
    Task<AuditReport> AnalyzeCodeAsync(SourceFile file, IEnumerable<AuditRule> rules, Action<string> onTokenReceived);
}