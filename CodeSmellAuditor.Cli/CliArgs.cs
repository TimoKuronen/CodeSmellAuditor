namespace CodeSmellAuditor.Cli;

public enum CliMode
{
    Batch,
    Sniff,
    SniffSystem
}

public sealed record CliArgs(
    CliMode Mode,
    IReadOnlyList<string> SniffPaths,
    string? Model = null,
    string? Storage = null,
    bool NonInteractive = false,
    string? Manifest = null)
{
    public static CliArgs Parse(string[] args)
    {
        string? model = null;
        string? storage = null;
        string? manifest = null;
        bool nonInteractive = false;
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];

            if (string.Equals(token, "--model", StringComparison.OrdinalIgnoreCase))
            {
                model = RequireOptionValue(args, ref i, "--model");
                continue;
            }

            if (string.Equals(token, "--storage", StringComparison.OrdinalIgnoreCase))
            {
                storage = RequireOptionValue(args, ref i, "--storage");
                continue;
            }

            if (string.Equals(token, "--manifest", StringComparison.OrdinalIgnoreCase))
            {
                manifest = RequireOptionValue(args, ref i, "--manifest");
                continue;
            }

            if (string.Equals(token, "--non-interactive", StringComparison.OrdinalIgnoreCase))
            {
                nonInteractive = true;
                continue;
            }

            if (token.StartsWith('-'))
            {
                throw new ArgumentException(
                    $"Unknown option '{token}'. Supported: --model, --storage, --manifest, --non-interactive.");
            }

            positional.Add(token);
        }

        if (positional.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(manifest))
            {
                throw new ArgumentException("--manifest is only valid with sniff-system.");
            }

            return new CliArgs(CliMode.Batch, Array.Empty<string>(), model, storage, nonInteractive);
        }

        if (string.Equals(positional[0], "sniff", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(manifest))
            {
                throw new ArgumentException("--manifest is only valid with sniff-system.");
            }

            IReadOnlyList<string> sniffPaths = ParseCsPaths(positional, minCount: 1, commandName: "sniff");
            return new CliArgs(CliMode.Sniff, sniffPaths, model, storage, nonInteractive);
        }

        if (string.Equals(positional[0], "sniff-system", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<string> systemPaths = ParseCsPaths(positional, minCount: 2, commandName: "sniff-system");
            return new CliArgs(CliMode.SniffSystem, systemPaths, model, storage, nonInteractive, manifest);
        }

        throw new ArgumentException(
            $"Unknown command '{positional[0]}'. Use no args for Targets batch, sniff <path.cs> [...], or sniff-system <path.cs> <path.cs> [...].");
    }

    private static IReadOnlyList<string> ParseCsPaths(List<string> positional, int minCount, string commandName)
    {
        if (positional.Count < minCount + 1)
        {
            throw new ArgumentException(
                minCount == 1
                    ? $"{commandName} requires at least one path to a .cs file."
                    : $"{commandName} requires at least two paths to .cs files.");
        }

        var paths = new List<string>();
        for (int i = 1; i < positional.Count; i++)
        {
            string path = positional[i].Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"{commandName} path arguments must not be empty.");
            }

            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{commandName} path must be a .cs file: {path}");
            }

            paths.Add(path);
        }

        return paths;
    }

    private static string RequireOptionValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
        {
            throw new ArgumentException($"{optionName} requires a value.");
        }

        index++;
        string value = args[index].Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{optionName} requires a value.");
        }

        return value;
    }
}
