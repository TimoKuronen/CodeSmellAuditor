using System.Globalization;

namespace CodeSmellAuditor.Eval;

/// <summary>
/// Append-only CSV store for results rows. Values never contain commas (labels are sanitized).
/// </summary>
public static class ResultsCsv
{
    private sealed record Column(string Name, Func<ResultRow, string> Get, Action<ResultRow, string> Set);

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly Column[] Columns =
    [
        Text("RunId", r => r.RunId, (r, v) => r.RunId = v),
        Text("DateUtc", r => r.DateUtc, (r, v) => r.DateUtc = v),
        Text("Label", r => r.Label, (r, v) => r.Label = v),
        Text("Model", r => r.Model, (r, v) => r.Model = v),
        Text("GitSha", r => r.GitSha, (r, v) => r.GitSha = v),
        Text("Dirty", r => r.Dirty ? "1" : "0", (r, v) => r.Dirty = v == "1"),
        Whole("Repeats", r => r.Repeats, (r, v) => r.Repeats = v),
        Text("CorpusHash", r => r.CorpusHash, (r, v) => r.CorpusHash = v),
        Text("PromptHash", r => r.PromptHash, (r, v) => r.PromptHash = v),
        Text("RulesHash", r => r.RulesHash, (r, v) => r.RulesHash = v),
        Text("CoreHash", r => r.CoreHash, (r, v) => r.CoreHash = v),
        Whole("BlatantTotal", r => r.BlatantTotal, (r, v) => r.BlatantTotal = v),
        Whole("JudgmentTotal", r => r.JudgmentTotal, (r, v) => r.JudgmentTotal = v),
        Number("ModelBlatant", r => r.ModelBlatant, (r, v) => r.ModelBlatant = v),
        Number("ModelJudgment", r => r.ModelJudgment, (r, v) => r.ModelJudgment = v),
        Number("PreCheckHits", r => r.PreCheckHits, (r, v) => r.PreCheckHits = v),
        Number("CombinedBlatant", r => r.CombinedBlatant, (r, v) => r.CombinedBlatant = v),
        Number("CombinedBlatantMin", r => r.CombinedBlatantMin, (r, v) => r.CombinedBlatantMin = v),
        Number("CombinedBlatantMax", r => r.CombinedBlatantMax, (r, v) => r.CombinedBlatantMax = v),
        Number("CombinedJudgment", r => r.CombinedJudgment, (r, v) => r.CombinedJudgment = v),
        Number("CombinedJudgmentMin", r => r.CombinedJudgmentMin, (r, v) => r.CombinedJudgmentMin = v),
        Number("CombinedJudgmentMax", r => r.CombinedJudgmentMax, (r, v) => r.CombinedJudgmentMax = v),
        Number("Noise", r => r.Noise, (r, v) => r.Noise = v),
        Number("NoiseMin", r => r.NoiseMin, (r, v) => r.NoiseMin = v),
        Number("NoiseMax", r => r.NoiseMax, (r, v) => r.NoiseMax = v),
        Number("Ungrounded", r => r.Ungrounded, (r, v) => r.Ungrounded = v),
        Number("CleanFindings", r => r.CleanFindings, (r, v) => r.CleanFindings = v),
        Number("Truncated", r => r.Truncated, (r, v) => r.Truncated = v),
        Number("Failed", r => r.Failed, (r, v) => r.Failed = v),
        Number("StatusMismatches", r => r.StatusMismatches, (r, v) => r.StatusMismatches = v),
        Number("SecondsPerFile", r => r.SecondsPerFile, (r, v) => r.SecondsPerFile = v),
        Number("PromptTokens", r => r.PromptTokens, (r, v) => r.PromptTokens = v),
        Number("OutputTokens", r => r.OutputTokens, (r, v) => r.OutputTokens = v),
        Whole("MinHeadroomTokens", r => r.MinHeadroomTokens, (r, v) => r.MinHeadroomTokens = v),
        Number("ClaudeSummarizeTokens", r => r.ClaudeSummarizeTokens, (r, v) => r.ClaudeSummarizeTokens = v),
        Number("ClaudeSidecarTokens", r => r.ClaudeSidecarTokens, (r, v) => r.ClaudeSidecarTokens = v),
        Number("ClaudeVerifyTokens", r => r.ClaudeVerifyTokens, (r, v) => r.ClaudeVerifyTokens = v)
    ];

    public static string Header => string.Join(',', Columns.Select(c => c.Name));

    public static string ToLine(ResultRow row) => string.Join(',', Columns.Select(c => c.Get(row)));

    public static ResultRow ParseLine(string line)
    {
        string[] fields = line.Split(',');
        if (fields.Length != Columns.Length)
        {
            throw new FormatException($"Expected {Columns.Length} columns but found {fields.Length}.");
        }

        var row = new ResultRow();
        for (int i = 0; i < Columns.Length; i++)
        {
            Columns[i].Set(row, fields[i]);
        }

        return row;
    }

    public static IReadOnlyList<ResultRow> Load(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<ResultRow>();
        }

        return File.ReadAllLines(path)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseLine)
            .ToList();
    }

    public static void Append(string path, ResultRow row)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bool exists = File.Exists(path) && new FileInfo(path).Length > 0;
        using var writer = new StreamWriter(path, append: true, new System.Text.UTF8Encoding(false));
        if (!exists)
        {
            writer.WriteLine(Header);
        }

        writer.WriteLine(ToLine(row));
    }

    private static Column Text(string name, Func<ResultRow, string> get, Action<ResultRow, string> set) =>
        new(name, get, set);

    private static Column Number(string name, Func<ResultRow, double> get, Action<ResultRow, double> set) =>
        new(name, r => get(r).ToString("0.##", Invariant), (r, v) => set(r, double.Parse(v, Invariant)));

    private static Column Whole(string name, Func<ResultRow, int> get, Action<ResultRow, int> set) =>
        new(name, r => get(r).ToString(Invariant), (r, v) => set(r, int.Parse(v, Invariant)));
}
