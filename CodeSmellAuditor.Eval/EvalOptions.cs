namespace CodeSmellAuditor.Eval;

/// <summary>
/// Command-line options. The model is pinned here, not taken from the CLI default, so a change
/// to the tool's default model cannot silently change the benchmark series.
/// </summary>
public sealed record EvalOptions(
    string Model,
    int Repeats,
    string Label,
    string? Only,
    string? Root)
{
    public const string PinnedModel = "qwen3.5:9b";
    public const int DefaultRepeats = 3;

    public bool IsPartial => !string.IsNullOrWhiteSpace(Only);

    public static EvalOptions Parse(string[] args)
    {
        string model = PinnedModel;
        int repeats = DefaultRepeats;
        string label = string.Empty;
        string? only = null;
        string? root = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--model":
                    model = Value(args, ref i);
                    break;
                case "--repeats":
                    if (!int.TryParse(Value(args, ref i), out repeats) || repeats < 1)
                    {
                        throw new ArgumentException("--repeats requires a positive integer.");
                    }

                    break;
                case "--label":
                    label = Value(args, ref i);
                    break;
                case "--only":
                    only = Value(args, ref i);
                    break;
                case "--root":
                    root = Value(args, ref i);
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown option '{args[i]}'. Supported: --model, --repeats, --label, --only, --root.");
            }
        }

        return new EvalOptions(model, repeats, label, only, root);
    }

    private static string Value(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{args[index]} requires a value.");
        }

        index++;
        return args[index].Trim().Trim('"');
    }
}
