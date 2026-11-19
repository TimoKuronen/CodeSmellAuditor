using System.Text;
using System.Text.RegularExpressions;

namespace CodeSmellAuditor.Fix;

/// <summary>
/// Private field naming rule shared with the pre-check: camelCase, with no capital first letter and
/// no m_, s_ or k_ prefix.
/// </summary>
public static class FieldNameConvention
{
    private static readonly Regex DisallowedStart = new(@"^(?:[A-Z]|[msk]_)", RegexOptions.Compiled);
    private static readonly Regex KindPrefix = new(@"^[msk]_", RegexOptions.Compiled);

    public static bool IsDisallowed(string name) => DisallowedStart.IsMatch(name);

    /// <summary>
    /// Returns the camelCase form of a disallowed name, or the name unchanged when no sensible form exists.
    /// </summary>
    public static string ToCamelCase(string name)
    {
        string core = KindPrefix.Replace(name, string.Empty);
        string[] parts = core.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return name;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            builder.Append(i == 0 ? LowerLeading(parts[i]) : char.ToUpperInvariant(parts[i][0]) + parts[i][1..]);
        }

        return builder.ToString();
    }

    private static string LowerLeading(string part)
    {
        int run = 0;
        while (run < part.Length && char.IsUpper(part[run]))
        {
            run++;
        }

        if (run == 0)
        {
            return part;
        }

        bool acronymBeforeWord = run > 1 && run < part.Length && char.IsLower(part[run]);
        int lowered = acronymBeforeWord ? run - 1 : run;
        return part[..lowered].ToLowerInvariant() + part[lowered..];
    }
}
