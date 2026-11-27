using System.Text;
using CodeSmellAuditor.Cli;
using CodeSmellAuditor.Core;
using CodeSmellAuditor.Fix;
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
    Environment.ExitCode = 2;
    return;
}

if (cliArgs.Mode == CliMode.Fix)
{
    try
    {
        Environment.ExitCode = await new CliFixHost(new FixEngine(FixerRegistry.CreateDefault())).RunAsync(cliArgs);
    }
    catch (Exception ex) when (ex is FileNotFoundException or ArgumentException or DirectoryNotFoundException)
    {
        AnsiConsole.MarkupLine($"[bold red]ERROR:[/] {Markup.Escape(ex.Message)}");
        Environment.ExitCode = 2;
    }

    return;
}

AppConfiguration appConfig = AppConfiguration.Resolve(
    cliArgs.Storage,
    cliArgs.Model,
    fixModel: null,
    cliArgs.Stack,
    Environment.GetEnvironmentVariable,
    AppContext.BaseDirectory);
string baseStoragePath = appConfig.StoragePath;
string rulesPath = appConfig.RulesPath;
string targetsPath = appConfig.TargetsPath;

if (!Directory.Exists(rulesPath))
{
    AnsiConsole.MarkupLine("[bold red]ERROR:[/] Rules folder not found.");
    AnsiConsole.MarkupLine($"[grey]Looked in:[/] {baseStoragePath}");
    AnsiConsole.MarkupLine("[grey]Set CODESMELL_STORAGE or --storage, or run from the repo with WorkstationStorage present.[/]");
    WaitForEnterIfInteractive(cliArgs);
    Environment.ExitCode = 2;
    return;
}

if (cliArgs.Mode == CliMode.Batch && !Directory.Exists(targetsPath))
{
    AnsiConsole.MarkupLine("[bold red]ERROR:[/] Targets folder not found.");
    AnsiConsole.MarkupLine($"[grey]Looked in:[/] {baseStoragePath}");
    AnsiConsole.MarkupLine("[grey]Set CODESMELL_STORAGE or --storage, or run from the repo with WorkstationStorage present.[/]");
    WaitForEnterIfInteractive(cliArgs);
    Environment.ExitCode = 2;
    return;
}

static void WaitForEnterIfInteractive(CliArgs parsed)
{
    if (parsed.NonInteractive || parsed.Mode is CliMode.Sniff or CliMode.SniffSystem)
    {
        return;
    }

    AnsiConsole.MarkupLine("[bold cyan]Press [[ENTER]] to exit...[/]");
    Console.ReadLine();
}

AuditConfiguration auditConfig = cliArgs.Mode == CliMode.SniffSystem
    ? appConfig.ForSystemAudit()
    : appConfig.ForFileAudit();

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(auditConfig.OllamaBaseAddress),
    Timeout = TimeSpan.FromMinutes(10)
};

IRuleRepository repository = new MarkdownRuleRepository();
IAiOrchestrator aiService = new OllamaAiOrchestrator(new OllamaChatClient(httpClient), auditConfig);
var engine = new AuditEngine(repository, aiService);
var host = new CliAuditHost(engine);

if (!string.IsNullOrWhiteSpace(appConfig.Stack))
{
    AnsiConsole.MarkupLine($"[grey]Stack pack:[/] [cyan]{Markup.Escape(appConfig.Stack)}[/]");
}

try
{
    if (cliArgs.Mode == CliMode.Batch)
    {
        AuditRunResult batchResult = await host.RunAsync(rulesPath, targetsPath, appConfig.Stack);
        Environment.ExitCode = batchResult.ExitCode;
        AnsiConsole.MarkupLine(
            batchResult.AllPassed
                ? "[bold green]Batch processing complete.[/]"
                : "[bold yellow]Batch processing complete with failures.[/]");
        WaitForEnterIfInteractive(cliArgs);
        return;
    }

    if (cliArgs.Mode == CliMode.SniffSystem)
    {
        AuditRunResult systemResult = await host.RunSniffSystemAsync(
            rulesPath,
            cliArgs.SniffPaths,
            cliArgs.Manifest,
            appConfig.Stack);
        Environment.ExitCode = systemResult.ExitCode;
        return;
    }

    AuditRunResult sniffResult = await host.RunSniffAsync(rulesPath, cliArgs.SniffPaths, appConfig.Stack);
    Environment.ExitCode = sniffResult.ExitCode;
}
catch (Exception ex) when (ex is FileNotFoundException or ArgumentException or DirectoryNotFoundException or InvalidOperationException)
{
    AnsiConsole.MarkupLine($"[bold red]ERROR:[/] {Markup.Escape(ex.Message)}");
    Environment.ExitCode = 2;
}
