using System.Text.RegularExpressions;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Lexical pass over scanned C# source that lists type declarations and the member statements
/// directly inside type bodies. Braces are tracked to tell type bodies from method and property
/// bodies; it is not a parser, so exotic layouts may be missed but are never invented.
/// </summary>
internal static class CSharpMemberScanner
{
    internal sealed record TypeScope(string Name, string Keyword, bool IsStatic);

    internal sealed record Declaration(
        string Text,
        int Line,
        bool OpensBlock,
        bool IsTopLevel,
        TypeScope? Owner,
        TypeScope? Declared);

    private sealed record Scope(TypeScope? Type, bool IsNamespace);

    private static readonly Regex NamespaceHeader = new(@"\bnamespace\b", RegexOptions.Compiled);

    private static readonly Regex TypeHeader = new(
        @"^(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|file|new|ref)\s+)*"
        + @"(?<keyword>class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(?<name>@?[A-Za-z_]\w*)",
        RegexOptions.Compiled);

    public static IReadOnlyList<Declaration> Scan(IReadOnlyList<CSharpSourceScanner.ScannedLine> lines)
    {
        var declarations = new List<Declaration>();
        var scopes = new Stack<Scope>();
        string header = string.Empty;
        int headerLine = 0;

        void FlushHeader(bool opensBlock)
        {
            string text = header.Trim();
            bool topLevel = scopes.All(scope => scope.IsNamespace);
            TypeScope? owner = scopes.Count > 0 ? scopes.Peek().Type : null;
            Scope? pushed = null;

            if (text.Length > 0)
            {
                bool isNamespace = NamespaceHeader.IsMatch(text);
                string bare = WithoutAttributes(text);
                Match typeMatch = TypeHeader.Match(bare);

                if (isNamespace)
                {
                    pushed = new Scope(null, true);
                }
                else if (typeMatch.Success)
                {
                    string keyword = Regex.Replace(typeMatch.Groups["keyword"].Value, @"\s+", " ");
                    var declared = new TypeScope(
                        typeMatch.Groups["name"].Value.TrimStart('@'),
                        keyword,
                        Regex.IsMatch(bare[..typeMatch.Groups["keyword"].Index], @"\bstatic\b"));
                    declarations.Add(new Declaration(text, headerLine, opensBlock, topLevel, owner, declared));
                    pushed = new Scope(declared, false);
                }
                else if (owner is not null)
                {
                    declarations.Add(new Declaration(text, headerLine, opensBlock, false, owner, null));
                }
            }

            if (opensBlock)
            {
                scopes.Push(pushed ?? new Scope(null, false));
            }

            header = string.Empty;
            headerLine = 0;
        }

        foreach (CSharpSourceScanner.ScannedLine line in lines)
        {
            if (line.Code.TrimStart().StartsWith('#'))
            {
                continue;
            }

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

        return declarations;
    }

    /// <summary>Removes leading attribute lists such as <c>[SerializeField]</c> from a declaration.</summary>
    public static string WithoutAttributes(string text)
    {
        string rest = text.TrimStart();
        while (rest.StartsWith('['))
        {
            int depth = 0;
            int end = -1;
            for (int i = 0; i < rest.Length; i++)
            {
                if (rest[i] == '[')
                {
                    depth++;
                }
                else if (rest[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }

            if (end < 0)
            {
                break;
            }

            rest = rest[(end + 1)..].TrimStart();
        }

        return rest;
    }
}
