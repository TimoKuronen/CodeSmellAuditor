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

    /// <summary>
    /// Loads base rules from <paramref name="rulesPath"/>, then optionally concatenates
    /// an opt-in stack pack under Rules/Stacks/{stackName}.
    /// When <paramref name="stackRootRulesPath"/> is set (system audits), the stack folder
    /// is resolved from that root instead of from the architecture rules folder.
    /// </summary>
    public async Task<IReadOnlyList<AuditRule>> LoadRulesAsync(
        string rulesPath,
        string? stackName,
        string? stackRootRulesPath = null)
    {
        var combined = new List<AuditRule>(await LoadRulesAsync(rulesPath));

        if (string.IsNullOrWhiteSpace(stackName))
        {
            return combined;
        }

        string rootForStack = stackRootRulesPath ?? rulesPath;
        string stackRulesPath = ResolveStackRulesPath(rootForStack, stackName);
        if (!Directory.Exists(stackRulesPath))
        {
            throw new DirectoryNotFoundException(
                $"Stack rules folder not found: {stackRulesPath}");
        }

        IReadOnlyList<AuditRule> stackRules =
            (await _ruleRepository.GetActiveRulesAsync(stackRulesPath)).ToList();

        if (stackRules.Count == 0)
        {
            throw new InvalidOperationException(
                $"No rule packs found under stack folder: {stackRulesPath}");
        }

        combined.AddRange(stackRules);
        return combined;
    }

    /// <summary>
    /// Loads Architecture rule packs (and optional stack packs from the root Rules folder)
    /// for sniff-system audits.
    /// </summary>
    public async Task<IReadOnlyList<AuditRule>> LoadSystemRulesAsync(
        string rulesPath,
        string? stackName = null)
    {
        string architectureRulesPath = ResolveArchitectureRulesPath(rulesPath);
        if (!Directory.Exists(architectureRulesPath))
        {
            throw new DirectoryNotFoundException(
                $"Architecture rules folder not found: {architectureRulesPath}");
        }

        IReadOnlyList<AuditRule> architectureRules = await LoadRulesAsync(architectureRulesPath);
        if (architectureRules.Count == 0)
        {
            throw new InvalidOperationException(
                $"No architecture rule packs found under {architectureRulesPath}");
        }

        if (string.IsNullOrWhiteSpace(stackName))
        {
            return architectureRules;
        }

        return await LoadRulesAsync(
            architectureRulesPath,
            stackName,
            stackRootRulesPath: rulesPath);
    }

    public static string ResolveArchitectureRulesPath(string rulesPath)
    {
        return Path.Combine(rulesPath, "Architecture");
    }

    public static string ResolveStackRulesPath(string rulesPath, string stackName)
    {
        if (string.IsNullOrWhiteSpace(stackName))
        {
            throw new ArgumentException("Stack name must not be empty.", nameof(stackName));
        }

        string trimmed = stackName.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmed.Contains('/')
            || trimmed.Contains('\\')
            || trimmed.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Invalid stack name '{stackName}'. Use a single folder name such as Unity.",
                nameof(stackName));
        }

        return Path.Combine(rulesPath, "Stacks", trimmed);
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
        return Directory.GetFiles(targetsPath, "*.cs")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Resolves caller paths to existing absolute .cs files. Enforces a minimum count.
    /// </summary>
    public static IReadOnlyList<string> ResolveExistingCsPaths(
        IReadOnlyList<string> filePaths,
        int minimumCount,
        string commandName)
    {
        if (filePaths.Count < minimumCount)
        {
            throw new ArgumentException(
                minimumCount <= 1
                    ? $"{commandName} requires at least one path to a .cs file."
                    : $"{commandName} requires at least two paths to .cs files.",
                nameof(filePaths));
        }

        var absolutePaths = new List<string>(filePaths.Count);
        foreach (string filePath in filePaths)
        {
            string absolutePath = Path.GetFullPath(filePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    $"{commandName} target not found: {absolutePath}",
                    absolutePath);
            }

            if (!absolutePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"{commandName} path must be a .cs file: {absolutePath}");
            }

            absolutePaths.Add(absolutePath);
        }

        return absolutePaths;
    }

    public async Task<FileAuditResult> AuditFileAsync(
        string filePath,
        IReadOnlyList<AuditRule> rules,
        string reportsDirectoryPath,
        Action<string>? onTokenReceived = null)
    {
        string absolutePath = Path.GetFullPath(filePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException($"Audit target not found: {absolutePath}", absolutePath);
        }

        if (!absolutePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Audit path must be a .cs file: {absolutePath}");
        }

        string fileName = Path.GetFileName(absolutePath);
        string sourceCodeText = await File.ReadAllTextAsync(absolutePath);
        SourceFile currentTarget = new(absolutePath, sourceCodeText);

        AuditReport auditReport = await _aiService.AnalyzeCodeAsync(
            currentTarget,
            rules,
            token => onTokenReceived?.Invoke(token));

        return await ToFileAuditResultAsync(
            fileName,
            fileName,
            auditReport,
            reportsDirectoryPath);
    }

    public async Task<FileAuditResult> AuditSystemAsync(
        IReadOnlyList<string> filePaths,
        string? manifestText,
        IReadOnlyList<AuditRule> rules,
        string reportsDirectoryPath,
        Action<string>? onTokenReceived = null)
    {
        IReadOnlyList<string> absolutePaths = ResolveExistingCsPaths(
            filePaths,
            minimumCount: 2,
            commandName: "System audit");

        var sources = new List<SourceFile>(absolutePaths.Count);
        var fileNames = new List<string>(absolutePaths.Count);

        foreach (string absolutePath in absolutePaths)
        {
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
        return await ToFileAuditResultAsync(
            SystemAuditIdentity.ReportBaseName,
            SystemAuditIdentity.ReportBaseName + ".cs",
            auditReport,
            reportsDirectoryPath,
            targetLabel);
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

    private static async Task<FileAuditResult> ToFileAuditResultAsync(
        string resultFileName,
        string reportBaseFileName,
        AuditReport auditReport,
        string reportsDirectoryPath,
        string? targetFileLabel = null)
    {
        if (auditReport.Outcome == AuditOutcome.Failed)
        {
            return new FileAuditResult(resultFileName, string.Empty, AuditOutcome.Failed);
        }

        string writtenFilePath = await SaveReportToFileSystemAsync(
            reportsDirectoryPath,
            reportBaseFileName,
            auditReport.MarkdownCritique,
            targetFileLabel);

        return new FileAuditResult(resultFileName, writtenFilePath, auditReport.Outcome);
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
