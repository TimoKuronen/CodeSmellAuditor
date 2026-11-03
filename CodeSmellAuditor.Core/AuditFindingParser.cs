using System.Text.RegularExpressions;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Reads structured findings (Id, File, Line, Rule, Fix, Evidence, Issue) from one report section.
/// Tolerates the layout drift small local models produce: fields on one line or on indented or
/// bulleted continuation lines, and quotes or backticks around evidence.
/// </summary>
public static partial class AuditFindingParser
{
    private static readonly string[] FieldLabels = { "File:", "Line:", "Rule:", "Fix:", "Evidence:", "Issue:" };

    /// <summary>
    /// Returns the findings under <paramref name="sectionHeading"/>, an empty list when the section
    /// states there are none, or null when the section is missing.
    /// </summary>
    public static IReadOnlyList<AuditFinding>? Parse(string reportBody, string sectionHeading)
    {
        List<string>? sectionLines = ReadSection(reportBody, sectionHeading);
        if (sectionLines is null)
        {
            return null;
        }

        var blocks = new List<List<string>>();
        foreach (string rawLine in sectionLines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            bool isBullet = line.StartsWith("- ") || line.StartsWith("* ");
            string content = isBullet ? line[2..].Trim() : line;
            bool startsFinding = (isBullet && !StartsWithFieldLabel(content)) || FindingIdPattern().IsMatch(content);

            if (startsFinding)
            {
                blocks.Add(new List<string> { content });
            }
            else if (blocks.Count > 0)
            {
                blocks[^1].Add(content);
            }
        }

        var findings = new List<AuditFinding>();
        foreach (List<string> block in blocks)
        {
            AuditFinding? finding = ParseBlock(block, findings.Count + 1);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    private static AuditFinding? ParseBlock(List<string> block, int ordinal)
    {
        string? evidenceLine = block.FirstOrDefault(l => l.StartsWith("Evidence:", StringComparison.OrdinalIgnoreCase));
        string? issueLine = block.FirstOrDefault(l => l.StartsWith("Issue:", StringComparison.OrdinalIgnoreCase));

        string header = block[0];
        Match idMatch = FindingIdPattern().Match(header);
        string headerText = idMatch.Success ? header[idMatch.Length..].Trim() : header;

        if (evidenceLine is null && IsNoneStatement(headerText))
        {
            return null;
        }

        string fieldText = string.Join(
            " | ",
            block.Where(l => !ReferenceEquals(l, evidenceLine) && !ReferenceEquals(l, issueLine)));

        string? lineValue = MatchField(LinePattern(), fieldText);
        string? fixValue = MatchField(FixPattern(), fieldText)?.ToLowerInvariant();

        string issue = issueLine is not null
            ? issueLine["Issue:".Length..].Trim()
            : StripFieldSegments(headerText);

        return new AuditFinding(
            Id: idMatch.Success ? idMatch.Groups[1].Value.ToUpperInvariant() : $"F{ordinal}",
            File: StripQuotes(MatchField(FilePattern(), fieldText)),
            ReportedLine: int.TryParse(lineValue, out int line) ? line : null,
            Rule: StripQuotes(MatchField(RulePattern(), fieldText)),
            Fix: fixValue is "mechanical" or "judgment" ? fixValue : null,
            Evidence: evidenceLine is null ? string.Empty : StripQuotes(evidenceLine["Evidence:".Length..]) ?? string.Empty,
            Issue: issue);
    }

    private static List<string>? ReadSection(string reportBody, string sectionHeading)
    {
        if (string.IsNullOrWhiteSpace(reportBody))
        {
            return null;
        }

        string[] lines = reportBody.Replace("\r\n", "\n").Split('\n');
        int start = Array.FindIndex(
            lines,
            l => l.Trim().Equals(sectionHeading, StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            return null;
        }

        var section = new List<string>();
        for (int i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith('#'))
            {
                break;
            }

            section.Add(lines[i]);
        }

        return section;
    }

    private static bool StartsWithFieldLabel(string content) =>
        FieldLabels.Any(label => content.StartsWith(label, StringComparison.OrdinalIgnoreCase));

    private static bool IsNoneStatement(string text) =>
        text.StartsWith("None", StringComparison.OrdinalIgnoreCase) ||
        text.StartsWith("N/A", StringComparison.OrdinalIgnoreCase) ||
        text.StartsWith("No findings", StringComparison.OrdinalIgnoreCase);

    private static string? MatchField(Regex pattern, string text)
    {
        Match match = pattern.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string StripFieldSegments(string headerText)
    {
        string[] segments = headerText.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string[] prose = segments.Where(s => !StartsWithFieldLabel(s)).ToArray();
        return prose.Length > 0 ? string.Join(" ", prose) : headerText;
    }

    private static string? StripQuotes(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string trimmed = value.Trim().Trim('`').Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    [GeneratedRegex(@"^\[(F\d+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex FindingIdPattern();

    [GeneratedRegex(@"\bFile:\s*([^|]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FilePattern();

    [GeneratedRegex(@"\bLine:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LinePattern();

    [GeneratedRegex(@"\bRule:\s*([^|]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"\bFix:\s*([A-Za-z]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FixPattern();
}
