namespace CodeSmellAuditor.Cli;

public enum CliMode
{
    Batch,
    Sniff
}

public sealed record CliArgs(
    CliMode Mode,
    string? SniffPath,
    string? Model = null,
    string? Storage = null,
    bool NonInteractive = false)
{
    public static CliArgs Parse(string[] args)
    {
        string? model = null;
        string? storage = null;
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

            if (string.Equals(token, "--non-interactive", StringComparison.OrdinalIgnoreCase))
            {
                nonInteractive = true;
                continue;
            }

            if (token.StartsWith('-'))
            {
                throw new ArgumentException(
                    $"Unknown option '{token}'. Supported: --model, --storage, --non-interactive.");
            }

            positional.Add(token);
        }

        if (positional.Count == 0)
        {
            return new CliArgs(CliMode.Batch, SniffPath: null, model, storage, nonInteractive);
        }

        if (!string.Equals(positional[0], "sniff", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unknown command '{positional[0]}'. Use no args for Targets batch, or: sniff <path-to-file.cs>");
        }

        if (positional.Count < 2 || string.IsNullOrWhiteSpace(positional[1]))
        {
            throw new ArgumentException("sniff requires a path to a .cs file.");
        }

        if (positional.Count > 2)
        {
            throw new ArgumentException("sniff accepts exactly one path argument.");
        }

        string path = positional[1].Trim().Trim('"');
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"sniff path must be a .cs file: {path}");
        }

        return new CliArgs(CliMode.Sniff, path, model, storage, nonInteractive);
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
