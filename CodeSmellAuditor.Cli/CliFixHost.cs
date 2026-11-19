using CodeSmellAuditor.Fix;
using Spectre.Console;

namespace CodeSmellAuditor.Cli;

/// <summary>
/// Interactive style fix run: plans fixes for the given paths, shows the diff, asks once, then writes.
/// Exit code 0 when nothing is pending or everything planned was written, 1 when planned changes were
/// not written (dry run, declined, or a file changed meanwhile), 2 when a fix was rejected or failed.
/// </summary>
public sealed class CliFixHost
{
    private const string ApplyAll = "Apply all changes";
    private const string ReviewEach = "Review file by file";
    private const string Cancel = "Cancel";

    private readonly FixEngine _engine;

    public CliFixHost(FixEngine engine)
    {
        _engine = engine;
    }

    public async Task<int> RunAsync(CliArgs args)
    {
        IReadOnlyList<string> files = SourceFileEnumerator.Expand(args.FixPaths ?? Array.Empty<string>());
        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]WARNING:[/] No C# files found.");
            return 0;
        }

        IReadOnlyList<FileFix> plan = await AnsiConsole.Status()
            .StartAsync($"Checking {files.Count} file(s)...", _ => _engine.PlanAsync(files));

        string root = CommonRoot(files);
        IReadOnlyList<FileFix> changed = plan.Where(f => f.HasChanges).ToList();
        IReadOnlyList<FileFix> failed = plan.Where(f => f.Error is not null).ToList();

        PrintDiffs(changed, root);
        PrintSkips(plan, root);
        PrintFailures(failed, root);
        PrintSummary(plan, changed, root);

        if (changed.Count == 0)
        {
            return failed.Count > 0 ? 2 : 0;
        }

        IReadOnlyList<FileFix> chosen = Choose(args, changed, root);
        if (chosen.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Nothing written.[/]");
            return 1;
        }

        int notWritten = changed.Count - Write(chosen, root);
        if (failed.Count > 0)
        {
            return 2;
        }

        return notWritten > 0 ? 1 : 0;
    }

    private static IReadOnlyList<FileFix> Choose(CliArgs args, IReadOnlyList<FileFix> changed, string root)
    {
        if (args.Yes)
        {
            return changed;
        }

        if (args.DryRun || args.NonInteractive)
        {
            AnsiConsole.MarkupLine("[grey]Dry run: use --yes to write these changes.[/]");
            return Array.Empty<FileFix>();
        }

        int changeCount = changed.Sum(f => f.Changes.Count);
        string choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Write [bold]{changeCount}[/] change(s) in [bold]{changed.Count}[/] file(s)?")
                .AddChoices(ApplyAll, ReviewEach, Cancel));

        if (choice == ApplyAll)
        {
            return changed;
        }

        if (choice == Cancel)
        {
            return Array.Empty<FileFix>();
        }

        return changed
            .Where(f => AnsiConsole.Confirm($"Write {f.Changes.Count} change(s) to [cyan]{Markup.Escape(Relative(f.Source.Path, root))}[/]?"))
            .ToList();
    }

    private static int Write(IReadOnlyList<FileFix> chosen, string root)
    {
        int written = 0;
        foreach (FileFix fix in chosen)
        {
            ApplyResult result = FixApplier.Apply(fix);
            string name = Markup.Escape(Relative(result.Path, root));
            if (result.Written)
            {
                written++;
                AnsiConsole.MarkupLine($"[green]Wrote[/] {name}");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Skipped[/] {name}: {Markup.Escape(result.Reason ?? string.Empty)}");
            }
        }

        return written;
    }

    private static void PrintDiffs(IReadOnlyList<FileFix> changed, string root)
    {
        foreach (FileFix fix in changed)
        {
            AnsiConsole.MarkupLine($"\n[bold cyan]{Markup.Escape(Relative(fix.Source.Path, root))}[/]");
            foreach (DiffLine line in LineDiff.Compute(fix.Source.Content, fix.FixedContent, context: 1))
            {
                string text = Markup.Escape(line.Text);
                AnsiConsole.MarkupLine(line.Kind switch
                {
                    DiffKind.Removed => $"[red]{line.OldLine,5} - {text}[/]",
                    DiffKind.Added => $"[green]{line.NewLine,5} + {text}[/]",
                    DiffKind.Gap => "[grey]      ...[/]",
                    _ => $"[grey]{line.NewLine,5}   {text}[/]"
                });
            }
        }
    }

    private static void PrintSkips(IReadOnlyList<FileFix> plan, string root)
    {
        var skips = plan.SelectMany(f => f.Skips.Select(s => (File: f.Source.Path, Skip: s))).ToList();
        if (skips.Count == 0)
        {
            return;
        }

        AnsiConsole.MarkupLine("\n[bold yellow]Left unchanged[/]");
        foreach ((string file, FixSkip skip) in skips)
        {
            AnsiConsole.MarkupLine(
                $"  {Markup.Escape(Relative(file, root))}:{skip.Line} [bold]{Markup.Escape(skip.Symbol)}[/] {Markup.Escape(skip.Reason)}");
        }
    }

    private static void PrintFailures(IReadOnlyList<FileFix> failed, string root)
    {
        foreach (FileFix fix in failed)
        {
            AnsiConsole.MarkupLine(
                $"[bold red]Rejected[/] {Markup.Escape(Relative(fix.Source.Path, root))}: {Markup.Escape(fix.Error ?? string.Empty)}");
        }
    }

    private static void PrintSummary(IReadOnlyList<FileFix> plan, IReadOnlyList<FileFix> changed, string root)
    {
        int skipCount = plan.Sum(f => f.Skips.Count);
        AnsiConsole.MarkupLine(
            $"\n[bold]{plan.Count}[/] file(s) checked, [bold]{changed.Count}[/] with changes " +
            $"([bold]{changed.Sum(f => f.Changes.Count)}[/] edit(s)), [bold]{skipCount}[/] left unchanged.");
    }

    private static string CommonRoot(IReadOnlyList<string> files)
    {
        string root = Path.GetDirectoryName(files[0]) ?? string.Empty;
        foreach (string file in files.Skip(1))
        {
            while (root.Length > 0 && !file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                root = Path.GetDirectoryName(root) ?? string.Empty;
            }
        }

        return root;
    }

    private static string Relative(string path, string root) =>
        root.Length > 0 ? Path.GetRelativePath(root, path) : path;
}
