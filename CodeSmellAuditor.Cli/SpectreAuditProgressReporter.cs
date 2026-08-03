using CodeSmellAuditor.Core;
using Spectre.Console;

namespace CodeSmellAuditor.Cli;

public sealed class SpectreAuditProgressReporter : IAuditProgressReporter
{
    public void RulesLoaded(IReadOnlyList<AuditRule> rules)
    {
        AnsiConsole.MarkupLine($"[bold green]Loaded {rules.Count} audit rules.[/]");
        foreach (var rule in rules)
        {
            AnsiConsole.MarkupLine($" [grey]└──[/] [cyan]{rule.Name}[/]");
        }

        AnsiConsole.WriteLine();
    }

    public void NoTargetsFound()
    {
        AnsiConsole.MarkupLine("[bold yellow]WARNING:[/] No C# target files found.");
    }

    public async Task<AuditReport> RunFileAuditAsync(
        string fileName,
        Func<Action<string>, Task<AuditReport>> analyzeAsync)
    {
        AnsiConsole.WriteLine();

        AuditReport? auditReport = null;
        var headingPrinted = false;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle(Style.Parse("yellow bold"))
            .StartAsync($"Auditing [[{fileName}]]...", async ctx =>
            {
                auditReport = await analyzeAsync(token =>
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

        return auditReport ?? throw new InvalidOperationException("Audit did not produce a report.");
    }

    public void ReportSaved(string fileName, string reportPath)
    {
        AnsiConsole.MarkupLine($"[grey]└── Report saved:[/] [underline cyan]{reportPath}[/]\n");
    }
}
