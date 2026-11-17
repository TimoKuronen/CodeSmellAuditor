using System.Text;
using System.Text.RegularExpressions;
using Hit = (int Line, string Check, string Message, string? Evidence);

namespace CodeSmellAuditor.Core;

/// <summary>
/// Pre-checks decided from code and comment text patterns: empty catch blocks, hidden dependency
/// lookups, non-ASCII comment text, and regions. Stack-specific patterns apply only to their stack.
/// </summary>
internal static class CodePatternPreChecks
{
    private const string UnityStack = "Unity";

    private static readonly Regex EmptyCatch = new(
        @"\bcatch\b(?:\s*\([^)]*\))?(?:\s*when\s*\([^)]*\))?\s*\{\s*\}",
        RegexOptions.Compiled);

    private static readonly Regex LocatorAccess = new(@"\bServiceLocator\s*\.", RegexOptions.Compiled);

    private static readonly Regex UnityLookup = new(
        @"\b(?:FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType|FindGameObjectWithTag|FindGameObjectsWithTag)\b"
        + @"|\bGameObject\s*\.\s*Find\s*\("
        + @"|\bResources\s*\.\s*Load\w*\s*[<(]",
        RegexOptions.Compiled);

    private static readonly Regex Region = new(@"^\s*#\s*region\b(?<name>.*)$", RegexOptions.Compiled);

    private static readonly HashSet<string> StructuredRegionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Serialized Fields", "Lifecycle", "Public API", "Private Helpers"
    };

    public static IEnumerable<Hit> Run(IReadOnlyList<CSharpSourceScanner.ScannedLine> lines, string? stackName)
    {
        bool unity = string.Equals(stackName, UnityStack, StringComparison.OrdinalIgnoreCase);

        foreach (Hit hit in CheckEmptyCatch(lines))
        {
            yield return hit;
        }

        foreach (Hit hit in CheckHiddenDependencyLookups(lines, unity))
        {
            yield return hit;
        }

        foreach (Hit hit in CheckNonAsciiComments(lines))
        {
            yield return hit;
        }

        if (unity)
        {
            foreach (Hit hit in CheckRegions(lines))
            {
                yield return hit;
            }
        }
    }

    private static IEnumerable<Hit> CheckEmptyCatch(IReadOnlyList<CSharpSourceScanner.ScannedLine> lines)
    {
        var code = new StringBuilder();
        var lineStarts = new List<int>(lines.Count);
        foreach (CSharpSourceScanner.ScannedLine line in lines)
        {
            lineStarts.Add(code.Length);
            code.Append(line.Code).Append('\n');
        }

        foreach (Match match in EmptyCatch.Matches(code.ToString()))
        {
            int index = lineStarts.BinarySearch(match.Index);
            int lineNumber = index >= 0 ? index + 1 : ~index;
            yield return (
                lineNumber,
                SourcePreChecks.EmptyCatch,
                "Empty catch block swallows the exception; handle it, log it, or let it propagate.",
                lines[lineNumber - 1].Raw.Trim());
        }
    }

    private static IEnumerable<Hit> CheckHiddenDependencyLookups(
        IReadOnlyList<CSharpSourceScanner.ScannedLine> lines,
        bool unity)
    {
        foreach (CSharpSourceScanner.ScannedLine line in lines)
        {
            Match match = LocatorAccess.Match(line.Code);
            if (!match.Success && unity)
            {
                match = UnityLookup.Match(line.Code);
            }

            if (match.Success)
            {
                yield return (
                    line.Number,
                    SourcePreChecks.HiddenDependencyLookup,
                    $"Hidden dependency lookup ({Regex.Replace(match.Value, @"\s+", string.Empty)}); inject the dependency instead of resolving it at the call site.",
                    line.Raw.Trim());
            }
        }
    }

    private static IEnumerable<Hit> CheckNonAsciiComments(IReadOnlyList<CSharpSourceScanner.ScannedLine> lines)
    {
        List<CSharpSourceScanner.ScannedLine> affected = lines
            .Where(line => line.Comment.Any(c => c > 127))
            .ToList();

        if (affected.Count == 0)
        {
            yield break;
        }

        yield return (
            affected[0].Number,
            SourcePreChecks.NonAsciiComment,
            $"{affected.Count} comment line(s) contain non-ASCII characters; use plain ASCII in comments.",
            affected[0].Raw.Trim());
    }

    private static IEnumerable<Hit> CheckRegions(IReadOnlyList<CSharpSourceScanner.ScannedLine> lines)
    {
        List<CSharpSourceScanner.ScannedLine> regions = lines
            .Where(line =>
            {
                Match match = Region.Match(line.Code);
                return match.Success && !StructuredRegionNames.Contains(match.Groups["name"].Value.Trim());
            })
            .ToList();

        if (regions.Count == 0)
        {
            yield break;
        }

        yield return (
            regions[0].Number,
            SourcePreChecks.RegionBlock,
            $"{regions.Count} #region block(s) outside the fixed names (Serialized Fields, Lifecycle, Public API, Private Helpers); the default is no regions.",
            regions[0].Raw.Trim());
    }
}
