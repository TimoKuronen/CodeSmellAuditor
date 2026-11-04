using System.Text.RegularExpressions;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Checks each finding's Evidence quote against the audited source so callers can trust
/// grounded findings without re-reading the files. Matching ignores whitespace differences.
/// </summary>
public static partial class FindingGroundingValidator
{
    /// <summary>
    /// Shorter quotes (a lone brace, a keyword) match almost any file and prove nothing.
    /// </summary>
    public const int MinimumEvidenceLength = 12;

    public static IReadOnlyList<AuditFinding> Validate(
        IReadOnlyList<AuditFinding> findings,
        IReadOnlyList<SourceFile> sources)
    {
        return findings.Select(finding => ValidateOne(finding, sources)).ToList();
    }

    private static AuditFinding ValidateOne(AuditFinding finding, IReadOnlyList<SourceFile> sources)
    {
        string evidence = CollapseWhitespace(finding.Evidence);
        if (evidence.Length == 0)
        {
            return Ungrounded(finding, "no evidence quote");
        }

        if (evidence.Length < MinimumEvidenceLength)
        {
            return Ungrounded(finding, "evidence too short to verify");
        }

        IReadOnlyList<SourceFile> candidates = sources;
        if (!string.IsNullOrWhiteSpace(finding.File))
        {
            string wanted = SystemAuditPromptBuilder.GetDisplayFileName(finding.File);
            candidates = sources
                .Where(s => SystemAuditPromptBuilder.GetDisplayFileName(s.FilePath)
                    .Equals(wanted, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0)
            {
                return Ungrounded(finding, $"file not in audited set: {wanted}");
            }
        }

        string[] segments = SplitOnEllipsis(evidence);
        if (segments.Sum(s => s.Length) < MinimumEvidenceLength)
        {
            return Ungrounded(finding, "evidence too short to verify");
        }

        foreach (SourceFile source in candidates)
        {
            int? line = FindLine(source.Content, segments, finding.ReportedLine);
            // A quote spanning lines may match the whole file, but abbreviated quotes must stay on
            // one line so their gaps cannot bridge unrelated code.
            bool matched = line is not null ||
                           (segments.Length == 1 && CollapseWhitespace(source.Content).Contains(segments[0], StringComparison.Ordinal));
            if (!matched)
            {
                continue;
            }

            string? note = finding.ReportedLine is { } reported && line is { } found && reported != found
                ? $"reported line {reported}, found at {found}"
                : null;

            return finding with
            {
                File = SystemAuditPromptBuilder.GetDisplayFileName(source.FilePath),
                IsGrounded = true,
                VerifiedLine = line,
                GroundingNote = note
            };
        }

        string scope = candidates.Count == 1
            ? SystemAuditPromptBuilder.GetDisplayFileName(candidates[0].FilePath)
            : "the audited files";
        return Ungrounded(finding, $"evidence not found in {scope}");
    }

    /// <summary>
    /// Returns the 1-based line containing the evidence, preferring the occurrence nearest
    /// the line the model reported.
    /// </summary>
    private static int? FindLine(string content, string[] segments, int? reportedLine)
    {
        string[] lines = content.Replace("\r\n", "\n").Split('\n');
        int? best = null;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!ContainsInOrder(CollapseWhitespace(lines[i]), segments))
            {
                continue;
            }

            int lineNumber = i + 1;
            if (reportedLine is null)
            {
                return lineNumber;
            }

            if (best is null || Math.Abs(lineNumber - reportedLine.Value) < Math.Abs(best.Value - reportedLine.Value))
            {
                best = lineNumber;
            }
        }

        return best;
    }

    /// <summary>
    /// Models often abbreviate long signatures as <c>Method(...)</c>; an ellipsis is treated as a gap,
    /// so the remaining segments must appear in order.
    /// </summary>
    private static string[] SplitOnEllipsis(string evidence) =>
        evidence.Replace("…", "...")
            .Split("...", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static bool ContainsInOrder(string text, string[] segments)
    {
        int position = 0;
        foreach (string segment in segments)
        {
            int index = text.IndexOf(segment, position, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            position = index + segment.Length;
        }

        return true;
    }

    private static AuditFinding Ungrounded(AuditFinding finding, string reason) =>
        finding with { IsGrounded = false, VerifiedLine = null, GroundingNote = reason };

    private static string CollapseWhitespace(string text) =>
        WhitespacePattern().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
