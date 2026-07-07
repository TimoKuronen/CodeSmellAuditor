namespace CodeSmellAuditor.Core;

public class MarkdownRuleRepository : IRuleRepository
{
    public async Task<IEnumerable<AuditRule>> GetActiveRulesAsync(string rulesFolderPath)
    {
        if (!Directory.Exists(rulesFolderPath))
        {
            throw new DirectoryNotFoundException($"Rules directory not found: {rulesFolderPath}");
        }

        var loadedRules = new List<AuditRule>();

        var ruleFiles = Directory.EnumerateFiles(rulesFolderPath, "*.*")
            .Where(file => file.EndsWith(".mdc", StringComparison.OrdinalIgnoreCase));

        foreach (var filePath in ruleFiles)
        {
            var content = await File.ReadAllTextAsync(filePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            string ruleName = Path.GetFileName(filePath);
            string guidelineBody = StripFrontMatter(content.Trim());

            loadedRules.Add(new AuditRule(ruleName, guidelineBody));
        }

        return loadedRules;
    }

    public static string StripFrontMatter(string content)
    {
        if (!content.StartsWith("---"))
        {
            return content;
        }

        int endIndex = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return content;
        }

        return content[(endIndex + 3)..].Trim();
    }
}
