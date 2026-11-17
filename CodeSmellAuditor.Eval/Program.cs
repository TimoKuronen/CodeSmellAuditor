using System.Text.Json;
using CodeSmellAuditor.Core;
using CodeSmellAuditor.Eval;

EvalOptions options;
EvalPaths paths;
try
{
    options = EvalOptions.Parse(args);
    paths = EvalPaths.Locate(options.Root);
}
catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    return 2;
}

List<AnswerKey> keys = Directory.GetFiles(paths.FixturesDir, "*.key.json")
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .Select(AnswerKey.Load)
    .Where(key => !options.IsPartial || key.Fixture.Contains(options.Only!, StringComparison.OrdinalIgnoreCase))
    .ToList();

if (keys.Count == 0)
{
    Console.WriteLine("ERROR: no fixtures matched.");
    return 2;
}

string runId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{options.Model.Replace(':', '-')}";
string runDir = Path.Combine(paths.RunsDir, runId);

var config = new AuditConfiguration(ModelName: options.Model);
using var httpClient = new HttpClient
{
    BaseAddress = new Uri(config.OllamaBaseAddress),
    Timeout = TimeSpan.FromMinutes(10)
};

var runner = new EvalRunner(httpClient, config, paths);

Console.WriteLine($"Benchmark {runId}: {keys.Count} fixtures x {options.Repeats} repeats, model {options.Model}");

try
{
    Console.WriteLine("Loading model...");
    await runner.WarmUpAsync();
}
catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    return 2;
}

var repeats = new List<IReadOnlyList<FixtureRunResult>>();
for (int repeat = 1; repeat <= options.Repeats; repeat++)
{
    var results = new List<FixtureRunResult>();
    foreach (AnswerKey key in keys)
    {
        FixtureRunResult result = await runner.RunFixtureAsync(key, Path.Combine(runDir, $"r{repeat}"));
        FixtureScore score = result.Score;
        Console.WriteLine(
            $"[r{repeat}] {key.Fixture,-22} {result.Seconds,5:0.0}s  hits {score.CombinedHits}/{score.ItemTotal}" +
            $"  noise {score.Noise}  ungrounded {score.Ungrounded}" +
            (score.Failed ? "  FAILED" : string.Empty) +
            (score.Truncated ? "  TRUNCATED" : string.Empty) +
            (score.StatusMismatch && !score.Failed ? "  status mismatch" : string.Empty));
        results.Add(result);
    }

    repeats.Add(results);
}

Directory.CreateDirectory(runDir);
await File.WriteAllTextAsync(
    Path.Combine(runDir, "scores.json"),
    JsonSerializer.Serialize(repeats, new JsonSerializerOptions { WriteIndented = true }));

if (options.IsPartial)
{
    Console.WriteLine($"Partial run (--only); nothing recorded. Details: {runDir}");
    return 0;
}

(string sha, bool dirty) = GitInfo.Read(paths.Root);
var identity = new RunIdentity(
    runId,
    DateTime.UtcNow,
    options.Label,
    options.Model,
    sha,
    dirty,
    ContentHasher.HashDirectory(paths.FixturesDir, "*", SearchOption.TopDirectoryOnly),
    ContentHasher.Hash(paths.CoreDir, new[] { Path.Combine(paths.CoreDir, "AuditPromptBuilder.cs") }),
    ContentHasher.Hash(
        paths.RulesDir,
        Directory.GetFiles(paths.RulesDir, "*.mdc", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Architecture{Path.DirectorySeparatorChar}"))),
    ContentHasher.HashDirectory(paths.CoreDir, "*.cs"));

ResultRow row = RunAggregator.Aggregate(identity, repeats);
IReadOnlyList<ResultRow> existing = ResultsCsv.Load(paths.ResultsCsv);
ResultRow? previous = existing.LastOrDefault(r => r.Model == row.Model);

ResultsCsv.Append(paths.ResultsCsv, row);
File.WriteAllText(
    paths.ResultsMarkdown,
    ResultsMarkdown.Render(existing.Append(row).ToList()),
    new System.Text.UTF8Encoding(false));

Console.WriteLine();
Console.WriteLine(ResultsMarkdown.RenderDelta(previous, row));
Console.WriteLine($"Table: {paths.ResultsMarkdown}");
Console.WriteLine($"Run details: {runDir}");
return 0;
