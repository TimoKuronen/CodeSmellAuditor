using System.Text;
using CodeSmellAuditor.Cli;
using CodeSmellAuditor.Core;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

AnsiConsole.Write(new FigletText("CodeSmellAuditor").Color(Color.DeepSkyBlue1));
AnsiConsole.MarkupLine("[bold grey]Local C# audit utility[/]\n");

CliArgs cliArgs;
try
{
    cliArgs = CliArgs.Parse(args);
}
catch (ArgumentException ex)
{
    AnsiConsole.MarkupLine($"[bold red]ERROR:[/] {Markup.Escape(ex.Message)}");
    Environment.ExitCode = 1;
    return;
}

string baseStoragePath = ResolveStoragePath(cliArgs.Storage);
string rulesPath = Path.Combine(baseStoragePath, "Rules");
string targetsPath = Path.Combine(baseStoragePath, "Targets");

if (!Directory.Exists(rulesPath))
{
    AnsiConsole.MarkupLine("[bold red]ERROR:[/] Rules folder not found.");
    AnsiConsole.MarkupLine($"[grey]Looked in:[/] {baseStoragePath}");
    AnsiConsole.MarkupLine("[grey]Set CODESMELL_STORAGE or --storage, or run from the repo with WorkstationStorage present.[/]");
    WaitForEnterIfInteractive(cliArgs);
    Environment.ExitCode = 1;
    return;
}

if (cliArgs.Mode == CliMode.Batch && !Directory.Exists(targetsPath))
{
    AnsiConsole.MarkupLine("[bold red]ERROR:[/] Targets folder not found.");
    AnsiConsole.MarkupLine($"[grey]Looked in:[/] {baseStoragePath}");
    AnsiConsole.MarkupLine("[grey]Set CODESMELL_STORAGE or --storage, or run from the repo with WorkstationStorage present.[/]");
    WaitForEnterIfInteractive(cliArgs);
    Environment.ExitCode = 1;
    return;
}

static string ResolveStoragePath(string? cliStorage)
{
    string? envStorage = Environment.GetEnvironmentVariable("CODESMELL_STORAGE");
    if (!string.IsNullOrWhiteSpace(envStorage))
    {
        return envStorage;
    }

    if (!string.IsNullOrWhiteSpace(cliStorage))
    {
        return Path.GetFullPath(cliStorage);
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

static string ResolveModelName(string? cliModel)
{
    string? envModel = Environment.GetEnvironmentVariable("CODESMELL_MODEL");
    if (!string.IsNullOrWhiteSpace(envModel))
    {
        return envModel;
    }

    if (!string.IsNullOrWhiteSpace(cliModel))
    {
        return cliModel;
    }

    return AuditConfiguration.DefaultModelName;
}

static void WaitForEnterIfInteractive(CliArgs parsed)
{
    if (parsed.NonInteractive || parsed.Mode == CliMode.Sniff)
    {
        return;
    }

    AnsiConsole.MarkupLine("[bold cyan]Press [[ENTER]] to exit...[/]");
    Console.ReadLine();
}

var auditConfig = new AuditConfiguration(
    ModelName: ResolveModelName(cliArgs.Model),
    NumCtx: int.TryParse(Environment.GetEnvironmentVariable("CODESMELL_NUM_CTX"), out int ctx) ? ctx : 8192,
    NumPredict: int.TryParse(Environment.GetEnvironmentVariable("CODESMELL_NUM_PREDICT"), out int predict) ? predict : 1200);

IRuleRepository repository = new MarkdownRuleRepository();
IAiOrchestrator aiService = new OllamaAiOrchestrator(auditConfig);
var engine = new AuditEngine(repository, aiService);
var host = new CliAuditHost(engine);

if (cliArgs.Mode == CliMode.Batch)
{
    AuditRunResult batchResult = await host.RunAsync(rulesPath, targetsPath);
    Environment.ExitCode = batchResult.ExitCode;
    AnsiConsole.MarkupLine(
        batchResult.AllPassed
            ? "[bold green]Batch processing complete.[/]"
            : "[bold yellow]Batch processing complete with failures.[/]");
    WaitForEnterIfInteractive(cliArgs);
    return;
}

try
{
    AuditRunResult sniffResult = await host.RunSniffAsync(rulesPath, cliArgs.SniffPath!);
    Environment.ExitCode = sniffResult.ExitCode;
}
catch (Exception ex) when (ex is FileNotFoundException or ArgumentException)
{
    AnsiConsole.MarkupLine($"[bold red]ERROR:[/] {Markup.Escape(ex.Message)}");
    Environment.ExitCode = 1;
}
