namespace CodeSmellAuditor.Core;

public class MarkdownRuleRepository : IRuleRepository
{
    public async Task<IEnumerable<AuditRule>> GetActiveRulesAsync(string rulesFolderPath)
    {
        if (!Directory.Exists(rulesFolderPath))
        {
            throw new DirectoryNotFoundException($"Architect Guard: Rules directory not found: {rulesFolderPath}");
        }

        var loadedRules = new List<AuditRule>();
        
        var ruleFiles = Directory.EnumerateFiles(rulesFolderPath, "*.*")
            .Where(file => file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || 
                           file.EndsWith(".mdc", StringComparison.OrdinalIgnoreCase));

        foreach (var filePath in ruleFiles)
        {
            var content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content)) continue;

            string ruleName = Path.GetFileName(filePath);
            string guidelineBody = content.Trim();
            
            // Fixed: Pass exactly 2 arguments to match the streamlined record definition
            loadedRules.Add(new AuditRule(ruleName, guidelineBody));
        }

        return loadedRules;
    }
}