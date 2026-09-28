using CodeSmellAuditor.Core;
using Spectre.Console;

namespace CodeSmellAuditor.Cli;

/// <summary>
/// Interactive batch runner. Owns Spectre Status wrapping around each Core file audit.
/// </summary>
public sealed class CliAuditHost
{
    private readonly AuditEngine _engine;

    public CliAuditHost(AuditEngine engine)
    {
        _engine = engine;
    }

    public async Task<AuditRunResult> RunAsync(string rulesPath, string targetsPath)
    {
        IReadOnlyList<AuditRule> rules = await _engine.LoadRulesAsync(rulesPath);
        PrintRulesLoaded(rules);

        IReadOnlyList<string> targetFiles = AuditEngine.EnumerateTargetFiles(targetsPath);
        if (targetFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]WARNING:[/] No C# target files found.");
            AuditRunResult emptyResult = new(Array.Empty<FileAuditResult>());
            PrintBatchSummary(emptyResult);
            return emptyResult;
        }

        string reportsDirectoryPath = AuditEngine.ResolveReportsDirectory(rulesPath);
        var fileResults = new List<FileAuditResult>();

        foreach (string filePath in targetFiles)
        {
            string fileName = Path.GetFileName(filePath);
            FileAuditResult result = await AuditFileWithStatusAsync(
                filePath,
                fileName,
                rules,
                reportsDirectoryPath);

            AnsiConsole.MarkupLine($"[grey]└── Report saved:[/] [underline cyan]{result.ReportPath}[/]\n");
            fileResults.Add(result);
        }

        AuditRunResult runResult = new(fileResults);
        PrintBatchSummary(runResult);
        return runResult;
    }

    /// <summary>
    /// Audits one or more external .cs files in place. Rules load once; reports stay under auditor storage.
    /// </summary>
    public async Task<AuditRunResult> RunSniffAsync(string rulesPath, IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
        {
            throw new ArgumentException("sniff requires at least one path to a .cs file.");
        }

        var absolutePaths = new List<string>(filePaths.Count);
        foreach (string filePath in filePaths)
        {
            string absolutePath = Path.GetFullPath(filePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException($"sniff target not found: {absolutePath}", absolutePath);
            }

            if (!absolutePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"sniff path must be a .cs file: {absolutePath}");
            }

            absolutePaths.Add(absolutePath);
        }

        IReadOnlyList<AuditRule> rules = await _engine.LoadRulesAsync(rulesPath);
        PrintRulesLoaded(rules);

        string reportsDirectoryPath = AuditEngine.ResolveReportsDirectory(rulesPath);
        var fileResults = new List<FileAuditResult>(absolutePaths.Count);

        foreach (string absolutePath in absolutePaths)
        {
            string fileName = Path.GetFileName(absolutePath);
            FileAuditResult result = await AuditFileWithStatusAsync(
                absolutePath,
                fileName,
                rules,
                reportsDirectoryPath);

            AnsiConsole.MarkupLine($"[grey]└── Report saved:[/] [underline cyan]{result.ReportPath}[/]\n");
            fileResults.Add(result);
        }

        AuditRunResult runResult = new(fileResults);
        PrintBatchSummary(runResult);
        return runResult;
    }

    /// <summary>
    /// One combined architecture audit over multiple .cs files using Architecture rule packs.
    /// </summary>
    public async Task<AuditRunResult> RunSniffSystemAsync(
        string rulesPath,
        IReadOnlyList<string> filePaths,
        string? manifestPath)
    {
        if (filePaths.Count < 2)
        {
            throw new ArgumentException("sniff-system requires at least two paths to .cs files.");
        }

        var absolutePaths = new List<string>(filePaths.Count);
        foreach (string filePath in filePaths)
        {
            string absolutePath = Path.GetFullPath(filePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException($"sniff-system target not found: {absolutePath}", absolutePath);
            }

            if (!absolutePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"sniff-system path must be a .cs file: {absolutePath}");
            }

            absolutePaths.Add(absolutePath);
        }

        string? manifestText = null;
        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            string absoluteManifest = Path.GetFullPath(manifestPath);
            if (!File.Exists(absoluteManifest))
            {
                throw new FileNotFoundException($"Manifest not found: {absoluteManifest}", absoluteManifest);
            }

            manifestText = await File.ReadAllTextAsync(absoluteManifest);
            AnsiConsole.MarkupLine($"[grey]Manifest:[/] [cyan]{Markup.Escape(absoluteManifest)}[/]");
        }

        string architectureRulesPath = AuditEngine.ResolveArchitectureRulesPath(rulesPath);
        if (!Directory.Exists(architectureRulesPath))
        {
            throw new DirectoryNotFoundException(
                $"Architecture rules folder not found: {architectureRulesPath}");
        }

        IReadOnlyList<AuditRule> rules = await _engine.LoadRulesAsync(architectureRulesPath);
        if (rules.Count == 0)
        {
            throw new InvalidOperationException(
                $"No architecture rule packs found under {architectureRulesPath}");
        }

        PrintRulesLoaded(rules);

        string reportsDirectoryPath = AuditEngine.ResolveReportsDirectory(rulesPath);
        string fileListLabel = string.Join(", ", absolutePaths.Select(Path.GetFileName));

        AnsiConsole.WriteLine();
        FileAuditResult? result = null;
        var headingPrinted = false;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync("Auditing system...", async ctx =>
            {
                result = await _engine.AuditSystemAsync(
                    absolutePaths,
                    manifestText,
                    rules,
                    reportsDirectoryPath,
                    token =>
                    {
                        if (!headingPrinted)
                        {
                            ctx.Status("Streaming system audit response...");
                            AnsiConsole.Write(new Rule($"[yellow]SYSTEM AUDIT: {fileListLabel}[/]").LeftJustified());
                            AnsiConsole.WriteLine();
                            headingPrinted = true;
                        }

                        Console.Write(token);
                    });
            });

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
        AnsiConsole.WriteLine();

        FileAuditResult systemResult = result
            ?? throw new InvalidOperationException("System audit did not produce a result.");

        AnsiConsole.MarkupLine($"[grey]└── Report saved:[/] [underline cyan]{systemResult.ReportPath}[/]\n");

        AuditRunResult runResult = new(new[] { systemResult });
        PrintBatchSummary(runResult);
        return runResult;
    }

    private async Task<FileAuditResult> AuditFileWithStatusAsync(
        string filePath,
        string fileName,
        IReadOnlyList<AuditRule> rules,
        string reportsDirectoryPath)
    {
        AnsiConsole.WriteLine();

        FileAuditResult? result = null;
        var headingPrinted = false;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync($"Auditing [[{fileName}]]...", async ctx =>
            {
                result = await _engine.AuditFileAsync(
                    filePath,
                    rules,
                    reportsDirectoryPath,
                    token =>
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

        return result ?? throw new InvalidOperationException("Audit did not produce a result.");
    }

    private static void PrintRulesLoaded(IReadOnlyList<AuditRule> rules)
    {
        AnsiConsole.MarkupLine($"[bold green]Loaded {rules.Count} audit rules.[/]");
        foreach (var rule in rules)
        {
            AnsiConsole.MarkupLine($" [grey]└──[/] [cyan]{rule.Name}[/]");
        }

        AnsiConsole.WriteLine();
    }

    private static void PrintBatchSummary(AuditRunResult result)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]Batch summary[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine(BatchRunSummary.Format(result));
        AnsiConsole.WriteLine();
    }
}
