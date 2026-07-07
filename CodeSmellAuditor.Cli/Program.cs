
using System.Text;
using CodeSmellAuditor.Core;
using Spectre.Console;

Console.OutputEncoding = Encoding.UTF8;

AnsiConsole.Write(new FigletText("ARCHITECT-1").Color(Color.DeepSkyBlue1));
AnsiConsole.MarkupLine("[bold grey]Tier 0 Production Audit Sandbox: Live Engine Active[/]\n");

string baseStoragePath = Environment.GetEnvironmentVariable("CODESMELL_STORAGE")
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "WorkstationStorage"));
string obsidianRulesPath = Path.Combine(baseStoragePath, "Rules");
string targetsScriptsPath = Path.Combine(baseStoragePath, "Targets");

if (!Directory.Exists(targetsScriptsPath) || !Directory.Exists(obsidianRulesPath))
{
    AnsiConsole.MarkupLine("[bold red]FATAL WIRING ERROR:[/] Physical storage subdirectories are missing.");
    return;
}

// Composition Root - Wire up dependencies
IRuleRepository repository = new MarkdownRuleRepository();
IAiOrchestrator aiService = new OllamaAiOrchestrator("qwen3.5:4b");

// Instantiate the engine and execute
var engine = new AuditEngine(repository, aiService);
await engine.RunAsync(obsidianRulesPath, targetsScriptsPath);

AnsiConsole.MarkupLine("[bold green]Batch processing complete.[/]");
AnsiConsole.MarkupLine("[bold cyan]Press [[ENTER]] to terminate structural tracking workstation...[/]");
Console.ReadLine();