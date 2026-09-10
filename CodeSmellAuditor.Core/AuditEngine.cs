using System.Text;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Domain orchestration for loading rules, auditing one file, and saving reports.
/// Batch presentation (spinners, streaming layout) belongs in Cli.
/// </summary>
public class AuditEngine
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IAiOrchestrator _aiService;

    public AuditEngine(IRuleRepository ruleRepository, IAiOrchestrator aiService)
    {
        _ruleRepository = ruleRepository;
        _aiService = aiService;
    }

    public async Task<IReadOnlyList<AuditRule>> LoadRulesAsync(string rulesPath)
    {
        return (await _ruleRepository.GetActiveRulesAsync(rulesPath)).ToList();
    }

    public static string ResolveReportsDirectory(string rulesPath)
    {
        string basePath = Path.GetDirectoryName(rulesPath.TrimEnd(Path.DirectorySeparatorChar))
                          ?? throw new InvalidOperationException("Invalid base storage path.");
        string reportsDirectoryPath = Path.Combine(basePath, "Reports");

        if (!Directory.Exists(reportsDirectoryPath))
        {
            Directory.CreateDirectory(reportsDirectoryPath);
        }

        return reportsDirectoryPath;
    }

    public static IReadOnlyList<string> EnumerateTargetFiles(string targetsPath)
    {
        return Directory.GetFiles(targetsPath, "*.cs");
    }

    public async Task<FileAuditResult> AuditFileAsync(
        string filePath,
        IReadOnlyList<AuditRule> rules,
        string reportsDirectoryPath,
        Action<string>? onTokenReceived = null)
    {
        string fileName = Path.GetFileName(filePath);
        string sourceCodeText = await File.ReadAllTextAsync(filePath);
        SourceFile currentTarget = new(filePath, sourceCodeText);

        AuditReport auditReport = await _aiService.AnalyzeCodeAsync(
            currentTarget,
            rules,
            token => onTokenReceived?.Invoke(token));

        string writtenFilePath = await SaveReportToFileSystemAsync(
            reportsDirectoryPath,
            fileName,
            auditReport.MarkdownCritique);

        return new FileAuditResult(fileName, writtenFilePath, auditReport.HasPassed);
    }

    public async Task<AuditRunResult> RunBatchAsync(string rulesPath, string targetsPath)
    {
        IReadOnlyList<AuditRule> rules = await LoadRulesAsync(rulesPath);
        string reportsDirectoryPath = ResolveReportsDirectory(rulesPath);
        IReadOnlyList<string> targetFiles = EnumerateTargetFiles(targetsPath);

        if (targetFiles.Count == 0)
        {
            return new AuditRunResult(Array.Empty<FileAuditResult>());
        }

        var fileResults = new List<FileAuditResult>();
        foreach (string filePath in targetFiles)
        {
            fileResults.Add(await AuditFileAsync(filePath, rules, reportsDirectoryPath));
        }

        return new AuditRunResult(fileResults);
    }

    private static async Task<string> SaveReportToFileSystemAsync(
        string targetFolder,
        string targetFileName,
        string markdownContent)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string cleanFileName = $"{Path.GetFileNameWithoutExtension(targetFileName)}_{timestamp}_Critique.md";
        string fullOutputPath = Path.Combine(targetFolder, cleanFileName);

        var documentBuilder = new StringBuilder();
        documentBuilder.AppendLine("---");
        documentBuilder.AppendLine($"TargetFile: {targetFileName}");
        documentBuilder.AppendLine($"AuditDate: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        documentBuilder.AppendLine("Tags: [code-smell-audit]");
        documentBuilder.AppendLine("---");
        documentBuilder.AppendLine();
        documentBuilder.AppendLine(markdownContent);

        await File.WriteAllTextAsync(fullOutputPath, documentBuilder.ToString(), Encoding.UTF8);
        return fullOutputPath;
    }
}
