namespace CodeSmellAuditor.Cli;

public enum CliMode
{
    Batch,
    Sniff
}

public sealed record CliArgs(CliMode Mode, string? SniffPath)
{
    public static CliArgs Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliArgs(CliMode.Batch, SniffPath: null);
        }

        if (!string.Equals(args[0], "sniff", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unknown command '{args[0]}'. Use no args for Targets batch, or: sniff <path-to-file.cs>");
        }

        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            throw new ArgumentException("sniff requires a path to a .cs file.");
        }

        if (args.Length > 2)
        {
            throw new ArgumentException("sniff accepts exactly one path argument.");
        }

        string path = args[1].Trim().Trim('"');
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"sniff path must be a .cs file: {path}");
        }

        return new CliArgs(CliMode.Sniff, path);
    }
}
