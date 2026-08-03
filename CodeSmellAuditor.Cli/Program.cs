
using System.Text;
using CodeSmellAuditor.Cli;
using CodeSmellAuditor.Core;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

AnsiConsole.Write(new FigletText("CodeSmellAuditor").Color(Color.DeepSkyBlue1));
AnsiConsole.MarkupLine("[bold grey]Local C# audit utility[/]\n");

string baseStoragePath = ResolveStoragePath();
string rulesPath = Path.Combine(baseStoragePath, "Rules");
string targetsPath = Path.Combine(baseStoragePath, "Targets");

if (!Directory.Exists(targetsPath) || !Directory.Exists(rulesPath))
{
    AnsiConsole.MarkupLine("[bold red]ERROR:[/] Rules or Targets folder not found.");
    AnsiConsole.MarkupLine($"[grey]Looked in:[/] {baseStoragePath}");
    AnsiConsole.MarkupLine("[grey]Set CODESMELL_STORAGE to override, or run from the repo with WorkstationStorage present.[/]");
    WaitForExit();
    return;
}

static string ResolveStoragePath()
{
    string? configuredPath = Environment.GetEnvironmentVariable("CODESMELL_STORAGE");
    if (!string.IsNullOrWhiteSpace(configuredPath))
    {
        return configuredPath;
    }

    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        string candidate = Path.Combine(current.FullName, "WorkstationStorage");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WorkstationStorage"));
}

static void WaitForExit()
{
    AnsiConsole.MarkupLine("[bold cyan]Press [[ENTER]] to exit...[/]");
    Console.ReadLine();
}

var auditConfig = new AuditConfiguration(
    ModelName: Environment.GetEnvironmentVariable("CODESMELL_MODEL") ?? "qwen3.5:4b",
    NumCtx: int.TryParse(Environment.GetEnvironmentVariable("CODESMELL_NUM_CTX"), out int ctx) ? ctx : 8192,
    NumPredict: int.TryParse(Environment.GetEnvironmentVariable("CODESMELL_NUM_PREDICT"), out int predict) ? predict : 1200);

IRuleRepository repository = new MarkdownRuleRepository();
IAiOrchestrator aiService = new OllamaAiOrchestrator(auditConfig);
IAuditProgressReporter progressReporter = new SpectreAuditProgressReporter();

var engine = new AuditEngine(repository, aiService, progressReporter);
await engine.RunAsync(rulesPath, targetsPath);

AnsiConsole.MarkupLine("[bold green]Batch processing complete.[/]");
WaitForExit();
