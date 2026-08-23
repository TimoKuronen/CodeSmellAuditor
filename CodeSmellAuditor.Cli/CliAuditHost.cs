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
            return new AuditRunResult(Array.Empty<FileAuditResult>());
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

        return new AuditRunResult(fileResults);
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
}
