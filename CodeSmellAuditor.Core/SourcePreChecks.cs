using System.Text.RegularExpressions;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Exact, model-free checks for rules that can be decided from the source text alone:
/// one primary type per file, file name matching the primary type, unresolved WIP markers and
/// stubs, and blocks of commented-out code. Lexical only; no Roslyn.
/// </summary>
public static class SourcePreChecks
{
    public const string MultipleTopLevelTypes = "multiple-top-level-types";
    public const string FileNameMismatch = "file-name-mismatch";
    public const string WipMarker = "wip-marker";
    public const string CommentedOutCode = "commented-out-code";

    private const int MinimumCommentedCodeLines = 2;

    private static readonly Regex TypeDeclaration = new(
        @"(?<![\w.])(?:class|struct|interface|enum|record)\b(?:\s+(?:class|struct)\b)?\s+(?<name>@?[A-Za-z_]\w*)",
        RegexOptions.Compiled);

    private static readonly Regex NamespaceHeader = new(@"\bnamespace\b", RegexOptions.Compiled);
    private static readonly Regex WipComment = new(@"\b(TODO|FIXME|HACK|TEMP|WIP)\b", RegexOptions.Compiled);
    private static readonly Regex DisabledBlock = new(@"^\s*#\s*if\s+false\b", RegexOptions.Compiled);
    private static readonly Regex NotImplementedStub = new(@"\bNotImplementedException\b", RegexOptions.Compiled);
    private static readonly Regex ElseLine = new(@"^\}?\s*else\b", RegexOptions.Compiled);

    public static IReadOnlyList<PreCheckFinding> Run(SourceFile file)
    {
        IReadOnlyList<CSharpSourceScanner.ScannedLine> lines = CSharpSourceScanner.Scan(file.Content);

        var raw = new List<(int Line, string Check, string Message, string? Evidence)>();
        raw.AddRange(CheckTopLevelTypes(file.FilePath, lines));
        raw.AddRange(CheckWipMarkers(lines));
        raw.AddRange(CheckCommentedOutCode(lines));

        return raw
            .OrderBy(item => item.Line)
            .ThenBy(item => item.Check, StringComparer.Ordinal)
            .Select((item, index) => new PreCheckFinding(
                $"P{index + 1}",
                item.Check,
                item.Line,
                item.Message,
                item.Evidence))
            .ToList();
    }

    private static IEnumerable<(int Line, string Check, string Message, string? Evidence)> CheckTopLevelTypes(
        string filePath,
        IReadOnlyList<CSharpSourceScanner.ScannedLine> lines)
    {
        List<(string Name, int Line)> types = FindTopLevelTypes(lines);

        if (types.Count > 1)
        {
            string names = string.Join(", ", types.Select(t => t.Name));
            yield return (
                types[1].Line,
                MultipleTopLevelTypes,
                $"{types.Count} top-level types in one file ({names}); keep one primary type per file.",
                lines[types[1].Line - 1].Raw.Trim());
        }

        if (types.Count == 1)
        {
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string typeName = types[0].Name.TrimStart('@');
            bool matches = string.Equals(baseName, typeName, StringComparison.Ordinal)
                           || baseName.StartsWith(typeName + ".", StringComparison.Ordinal);

            if (!matches)
            {
                yield return (
                    types[0].Line,
                    FileNameMismatch,
                    $"File name '{baseName}' does not match its only type '{typeName}'.",
                    lines[types[0].Line - 1].Raw.Trim());
            }
        }
    }

    private static List<(string Name, int Line)> FindTopLevelTypes(
        IReadOnlyList<CSharpSourceScanner.ScannedLine> lines)
    {
        var types = new List<(string Name, int Line)>();
        var scopes = new Stack<bool>();
        string header = string.Empty;
        int headerLine = 0;

        void FlushHeader(bool opensBlock)
        {
            bool topLevel = scopes.All(isNamespace => isNamespace);
            bool isNamespace = NamespaceHeader.IsMatch(header);

            if (topLevel && !isNamespace && !header.TrimStart().StartsWith("using", StringComparison.Ordinal))
            {
                Match match = TypeDeclaration.Match(header);
                if (match.Success)
                {
                    types.Add((match.Groups["name"].Value, headerLine));
                }
            }

            if (opensBlock)
            {
                scopes.Push(isNamespace);
            }

            header = string.Empty;
            headerLine = 0;
        }

        foreach (CSharpSourceScanner.ScannedLine line in lines)
        {
            foreach (char c in line.Code)
            {
                if (c == '{')
                {
                    FlushHeader(opensBlock: true);
                }
                else if (c == '}')
                {
                    if (scopes.Count > 0)
                    {
                        scopes.Pop();
                    }

                    header = string.Empty;
                    headerLine = 0;
                }
                else if (c == ';')
                {
                    FlushHeader(opensBlock: false);
                }
                else
                {
                    if (headerLine == 0 && !char.IsWhiteSpace(c))
                    {
                        headerLine = line.Number;
                    }

                    header += c;
                }
            }

            header += ' ';
        }

        return types;
    }

    private static IEnumerable<(int Line, string Check, string Message, string? Evidence)> CheckWipMarkers(
        IReadOnlyList<CSharpSourceScanner.ScannedLine> lines)
    {
        foreach (CSharpSourceScanner.ScannedLine line in lines)
        {
            Match marker = WipComment.Match(line.Comment);
            if (marker.Success)
            {
                yield return (
                    line.Number,
                    WipMarker,
                    $"Unresolved {marker.Value} marker; resolve it or track it outside the code.",
                    line.Raw.Trim());
            }

            if (DisabledBlock.IsMatch(line.Code))
            {
                yield return (
                    line.Number,
                    WipMarker,
                    "Disabled code block (#if false).",
                    line.Raw.Trim());
            }

            if (NotImplementedStub.IsMatch(line.Code))
            {
                yield return (
                    line.Number,
                    WipMarker,
                    "NotImplementedException stub.",
                    line.Raw.Trim());
            }
        }
    }

    private static List<(int Line, string Check, string Message, string? Evidence)> CheckCommentedOutCode(
        IReadOnlyList<CSharpSourceScanner.ScannedLine> lines)
    {
        var findings = new List<(int Line, string Check, string Message, string? Evidence)>();
        int groupStart = 0;
        int groupLength = 0;

        void CloseGroup()
        {
            if (groupLength >= MinimumCommentedCodeLines)
            {
                findings.Add((
                    groupStart,
                    CommentedOutCode,
                    $"{groupLength} consecutive lines of commented-out code; delete them.",
                    lines[groupStart - 1].Raw.Trim()));
            }

            groupLength = 0;
        }

        foreach (CSharpSourceScanner.ScannedLine line in lines)
        {
            if (!IsCommentedOutCode(line))
            {
                CloseGroup();
                continue;
            }

            if (groupLength == 0)
            {
                groupStart = line.Number;
            }

            groupLength++;
        }

        CloseGroup();
        return findings;
    }

    private static bool IsCommentedOutCode(CSharpSourceScanner.ScannedLine line)
    {
        if (line.IsDocComment || !string.IsNullOrWhiteSpace(line.Code))
        {
            return false;
        }

        string text = line.Comment.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        return text.EndsWith(';')
               || text == "{"
               || text == "}"
               || ElseLine.IsMatch(text);
    }
}
