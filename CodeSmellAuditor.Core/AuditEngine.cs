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

    public static string ResolveArchitectureRulesPath(string rulesPath)
    {
        return Path.Combine(rulesPath, "Architecture");
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

    public async Task<FileAuditResult> AuditSystemAsync(
        IReadOnlyList<string> filePaths,
        string? manifestText,
        IReadOnlyList<AuditRule> rules,
        string reportsDirectoryPath,
        Action<string>? onTokenReceived = null)
    {
        if (filePaths.Count < 2)
        {
            throw new ArgumentException("System audit requires at least two .cs file paths.", nameof(filePaths));
        }

        var sources = new List<SourceFile>(filePaths.Count);
        var fileNames = new List<string>(filePaths.Count);

        foreach (string filePath in filePaths)
        {
            string absolutePath = Path.GetFullPath(filePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException($"System audit target not found: {absolutePath}", absolutePath);
            }

            if (!absolutePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"System audit path must be a .cs file: {absolutePath}");
            }

            string content = await File.ReadAllTextAsync(absolutePath);
            sources.Add(new SourceFile(absolutePath, content));
            fileNames.Add(Path.GetFileName(absolutePath));
        }

        AuditReport auditReport = await _aiService.AnalyzeSystemAsync(
            sources,
            manifestText,
            rules,
            token => onTokenReceived?.Invoke(token));

        string targetLabel = string.Join(", ", fileNames);
        string writtenFilePath = await SaveReportToFileSystemAsync(
            reportsDirectoryPath,
            SystemAuditIdentity.ReportBaseName + ".cs",
            auditReport.MarkdownCritique,
            targetLabel);

        return new FileAuditResult(SystemAuditIdentity.ReportBaseName, writtenFilePath, auditReport.HasPassed);
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
        string markdownContent,
        string? targetFileLabel = null)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string cleanFileName = $"{Path.GetFileNameWithoutExtension(targetFileName)}_{timestamp}_Critique.md";
        string fullOutputPath = Path.Combine(targetFolder, cleanFileName);

        var documentBuilder = new StringBuilder();
        documentBuilder.AppendLine("---");
        documentBuilder.AppendLine($"TargetFile: {targetFileLabel ?? targetFileName}");
        documentBuilder.AppendLine($"AuditDate: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        documentBuilder.AppendLine("Tags: [code-smell-audit]");
        documentBuilder.AppendLine("---");
        documentBuilder.AppendLine();
        documentBuilder.AppendLine(markdownContent);

        await File.WriteAllTextAsync(fullOutputPath, documentBuilder.ToString(), Encoding.UTF8);
        return fullOutputPath;
    }
}
