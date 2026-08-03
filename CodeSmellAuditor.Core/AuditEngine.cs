using System.Text;

namespace CodeSmellAuditor.Core;

public class AuditEngine
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IAiOrchestrator _aiService;
    private readonly IAuditProgressReporter _progressReporter;

    public AuditEngine(
        IRuleRepository ruleRepository,
        IAiOrchestrator aiService,
        IAuditProgressReporter progressReporter)
    {
        _ruleRepository = ruleRepository;
        _aiService = aiService;
        _progressReporter = progressReporter;
    }

    public async Task<AuditRunResult> RunAsync(string rulesPath, string targetsPath)
    {
        string basePath = Path.GetDirectoryName(rulesPath.TrimEnd(Path.DirectorySeparatorChar))
                          ?? throw new InvalidOperationException("Invalid base storage path.");
        string reportsDirectoryPath = Path.Combine(basePath, "Reports");

        if (!Directory.Exists(reportsDirectoryPath))
        {
            Directory.CreateDirectory(reportsDirectoryPath);
        }

        var architecturalRules = (await _ruleRepository.GetActiveRulesAsync(rulesPath)).ToList();
        _progressReporter.RulesLoaded(architecturalRules);

        var targetFiles = Directory.GetFiles(targetsPath, "*.cs");
        if (targetFiles.Length == 0)
        {
            _progressReporter.NoTargetsFound();
            return new AuditRunResult(Array.Empty<FileAuditResult>());
        }

        var fileResults = new List<FileAuditResult>();

        foreach (var filePath in targetFiles)
        {
            string fileName = Path.GetFileName(filePath);
            string sourceCodeText = await File.ReadAllTextAsync(filePath);
            SourceFile currentTarget = new(filePath, sourceCodeText);

            AuditReport auditReport = await _progressReporter.RunFileAuditAsync(
                fileName,
                onTokenReceived => _aiService.AnalyzeCodeAsync(
                    currentTarget,
                    architecturalRules,
                    onTokenReceived));

            string writtenFilePath = await SaveReportToFileSystemAsync(
                reportsDirectoryPath,
                fileName,
                auditReport.MarkdownCritique);

            _progressReporter.ReportSaved(fileName, writtenFilePath);

            fileResults.Add(new FileAuditResult(fileName, writtenFilePath, auditReport.HasPassed));
        }

        return new AuditRunResult(fileResults);
    }

    private static async Task<string> SaveReportToFileSystemAsync(
        string targetFolder,
        string targetFileName,
        string markdownContent)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string cleanFileName = $"{timestamp}_{Path.GetFileNameWithoutExtension(targetFileName)}_Critique.md";
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
