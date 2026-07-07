using System.Text;
using Spectre.Console;

namespace CodeSmellAuditor.Core;

public class AuditEngine
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IAiOrchestrator _aiService;

    public AuditEngine(IRuleRepository ruleRepository, IAiOrchestrator aiService)
    {
        _ruleRepository = ruleRepository;
        _aiService = aiService;
    }

    public async Task RunAsync(string rulesPath, string targetsPath)
    {
        string basePath = Path.GetDirectoryName(rulesPath.TrimEnd(Path.DirectorySeparatorChar))
                          ?? throw new InvalidOperationException("Invalid base storage path.");
        string reportsDirectoryPath = Path.Combine(basePath, "Reports");

        if (!Directory.Exists(reportsDirectoryPath))
        {
            Directory.CreateDirectory(reportsDirectoryPath);
        }

        var architecturalRules = (await _ruleRepository.GetActiveRulesAsync(rulesPath)).ToList();

        AnsiConsole.MarkupLine($"[bold green]Loaded {architecturalRules.Count} audit rules.[/]");
        foreach (var rule in architecturalRules)
        {
            AnsiConsole.MarkupLine($" [grey]└──[/] [cyan]{rule.Name}[/]");
        }
        AnsiConsole.WriteLine();

        var targetFiles = Directory.GetFiles(targetsPath, "*.cs");
        if (targetFiles.Length == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]WARNING:[/] No C# target files found.");
            return;
        }

        foreach (var filePath in targetFiles)
        {
            string fileName = Path.GetFileName(filePath);
            string sourceCodeText = await File.ReadAllTextAsync(filePath);
            SourceFile currentTarget = new(filePath, sourceCodeText);

            AnsiConsole.WriteLine();

            AuditReport? auditReport = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots2)
                .SpinnerStyle(Style.Parse("yellow bold"))
                .StartAsync($"Auditing [[{fileName}]]...", async ctx =>
                {
                    bool headingPrinted = false;

                    auditReport = await _aiService.AnalyzeCodeAsync(currentTarget, architecturalRules, token =>
                    {
                        if (!headingPrinted)
                        {
                            ctx.Status("Streaming audit response...");
                            AnsiConsole.Write(new Rule($"[yellow]AUDIT: {fileName}[/]").LeftJustified());
                            AnsiConsole.WriteLine();
                            headingPrinted = true;
                        }

                        Console.Write(token);
                    });
                });

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule().RuleStyle("grey"));
            AnsiConsole.WriteLine();

            if (auditReport != null)
            {
                string writtenFilePath = await SaveReportToFileSystemAsync(
                    reportsDirectoryPath,
                    fileName,
                    auditReport.MarkdownCritique);
                AnsiConsole.MarkupLine($"[grey]└── Report saved:[/] [underline cyan]{writtenFilePath}[/]\n");
            }
        }
    }

    private async Task<string> SaveReportToFileSystemAsync(
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
