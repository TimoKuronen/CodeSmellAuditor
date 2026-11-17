using System.Text.RegularExpressions;
using Hit = (int Line, string Check, string Message, string? Evidence);

namespace CodeSmellAuditor.Core;

/// <summary>
/// Pre-checks decided from type and member declarations: static mutable fields, private field
/// naming, constructor size, and enum-only files.
/// </summary>
internal static class DeclarationPreChecks
{
    private const int MaxConstructorParameters = 5;

    private static readonly HashSet<string> FieldModifiers = new(StringComparer.Ordinal)
    {
        "public", "private", "protected", "internal", "static", "readonly", "const", "volatile",
        "new", "unsafe", "extern", "required", "sealed", "override", "virtual", "abstract"
    };

    private static readonly HashSet<string> NonFieldKeywords = new(StringComparer.Ordinal)
    {
        "event", "delegate", "operator", "implicit", "explicit", "using", "return", "throw"
    };

    private static readonly Regex Initializer = new(@"(?<![=!<>])=(?![=>])", RegexOptions.Compiled);
    private static readonly Regex GenericArguments = new(@"<[^<>]*>", RegexOptions.Compiled);
    private static readonly Regex DisallowedFieldPrefix = new(@"^(?:[A-Z]|[msk]_)", RegexOptions.Compiled);

    private sealed record Field(
        IReadOnlyList<string> Names,
        bool IsStatic,
        bool IsReadOnly,
        bool IsConst,
        bool IsPrivate);

    public static IEnumerable<Hit> Run(
        IReadOnlyList<CSharpSourceScanner.ScannedLine> lines,
        IReadOnlyList<CSharpMemberScanner.Declaration> declarations)
    {
        foreach (CSharpMemberScanner.Declaration declaration in declarations)
        {
            Field? field = ParseField(declaration);
            if (field is not null)
            {
                foreach (Hit hit in CheckField(lines, declaration, field))
                {
                    yield return hit;
                }
            }

            int? parameterCount = ConstructorParameterCount(declaration);
            if (parameterCount > MaxConstructorParameters)
            {
                string typeName = (declaration.Declared ?? declaration.Owner)!.Name;
                int line = Locate(lines, declaration.Line, typeName);
                yield return (
                    line,
                    SourcePreChecks.ConstructorTooLarge,
                    $"Constructor of '{typeName}' takes {parameterCount} parameters; more than {MaxConstructorParameters} suggests too many dependencies.",
                    lines[line - 1].Raw.Trim());
            }
        }

        foreach (Hit hit in CheckEnumOnlyFile(lines, declarations))
        {
            yield return hit;
        }
    }

    private static IEnumerable<Hit> CheckField(
        IReadOnlyList<CSharpSourceScanner.ScannedLine> lines,
        CSharpMemberScanner.Declaration declaration,
        Field field)
    {
        CSharpMemberScanner.TypeScope owner = declaration.Owner!;

        foreach (string name in field.Names)
        {
            int line = Locate(lines, declaration.Line, name);
            string evidence = lines[line - 1].Raw.Trim();

            if (field.IsStatic && !field.IsReadOnly && !field.IsConst && !owner.IsStatic)
            {
                yield return (
                    line,
                    SourcePreChecks.StaticMutableField,
                    $"Static mutable field '{name}' in non-static type '{owner.Name}'; this is shared global state.",
                    evidence);
            }

            if (field.IsPrivate && !field.IsStatic && !field.IsConst && DisallowedFieldPrefix.IsMatch(name))
            {
                yield return (
                    line,
                    SourcePreChecks.PrivateFieldNaming,
                    $"Private field '{name}' is not camelCase.",
                    evidence);
            }
        }
    }

    private static Field? ParseField(CSharpMemberScanner.Declaration declaration)
    {
        if (declaration.Owner is null
            || declaration.Declared is not null
            || declaration.Owner.Keyword == "interface")
        {
            return null;
        }

        string text = CSharpMemberScanner.WithoutAttributes(declaration.Text);
        Match initializer = Initializer.Match(text);
        if (declaration.OpensBlock && !initializer.Success)
        {
            return null;
        }

        string head = initializer.Success ? text[..initializer.Index] : text;
        if (head.Contains('(') || head.Contains(')') || head.Contains("=>", StringComparison.Ordinal))
        {
            return null;
        }

        string flat = head;
        string previous;
        do
        {
            previous = flat;
            flat = GenericArguments.Replace(flat, string.Empty);
        }
        while (flat != previous);

        flat = Regex.Replace(flat, @"[\[\]?*]", " ");
        string[] tokens = flat.Split(new[] { ' ', '\t', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Any(NonFieldKeywords.Contains))
        {
            return null;
        }

        int index = 0;
        var modifiers = new HashSet<string>(StringComparer.Ordinal);
        while (index < tokens.Length && FieldModifiers.Contains(tokens[index]))
        {
            modifiers.Add(tokens[index]);
            index++;
        }

        // One type token, then at least one declarator name.
        if (tokens.Length - index < 2)
        {
            return null;
        }

        string[] names = tokens.Skip(index + 1).Select(name => name.TrimStart('@')).ToArray();
        bool hasOtherAccess = modifiers.Contains("public") || modifiers.Contains("protected") || modifiers.Contains("internal");

        return new Field(
            names,
            modifiers.Contains("static"),
            modifiers.Contains("readonly"),
            modifiers.Contains("const"),
            !hasOtherAccess);
    }

    private static int? ConstructorParameterCount(CSharpMemberScanner.Declaration declaration)
    {
        string text = CSharpMemberScanner.WithoutAttributes(declaration.Text);

        if (declaration.Declared is { } declared)
        {
            if (declared.Keyword is not ("class" or "struct"))
            {
                return null;
            }

            Match primary = Regex.Match(
                text,
                $@"\b(?:class|struct)\s+@?{Regex.Escape(declared.Name)}\s*(?:<[^()]*?>)?\s*\(");
            return primary.Success ? CountParameters(text, primary.Index + primary.Length - 1) : null;
        }

        if (declaration.Owner is null)
        {
            return null;
        }

        Match constructor = Regex.Match(
            text,
            $@"^(?:(?:public|private|protected|internal|static|extern|unsafe)\s+)*{Regex.Escape(declaration.Owner.Name)}\s*\(");
        return constructor.Success ? CountParameters(text, constructor.Length - 1) : null;
    }

    private static int CountParameters(string text, int openParenIndex)
    {
        int depth = 0;
        int count = 0;
        bool hasContent = false;

        for (int i = openParenIndex; i < text.Length; i++)
        {
            char c = text[i];

            if (c is '(' or '[' or '{' or '<')
            {
                depth++;
                if (depth > 1)
                {
                    hasContent = true;
                }

                continue;
            }

            if (c == '>' && i > 0 && text[i - 1] == '=')
            {
                hasContent = true;
                continue;
            }

            if (c is ')' or ']' or '}' or '>')
            {
                depth--;
                if (depth == 0)
                {
                    break;
                }

                continue;
            }

            if (c == ',' && depth == 1)
            {
                count++;
            }
            else if (!char.IsWhiteSpace(c))
            {
                hasContent = true;
            }
        }

        return hasContent ? count + 1 : 0;
    }

    private static IEnumerable<Hit> CheckEnumOnlyFile(
        IReadOnlyList<CSharpSourceScanner.ScannedLine> lines,
        IReadOnlyList<CSharpMemberScanner.Declaration> declarations)
    {
        List<CSharpMemberScanner.Declaration> topLevelTypes = declarations
            .Where(d => d.IsTopLevel && d.Declared is not null)
            .ToList();

        if (topLevelTypes.Count != 1 || topLevelTypes[0].Declared!.Keyword != "enum")
        {
            yield break;
        }

        CSharpMemberScanner.Declaration enumDeclaration = topLevelTypes[0];
        string name = enumDeclaration.Declared!.Name;
        string bare = CSharpMemberScanner.WithoutAttributes(enumDeclaration.Text);
        if (Regex.IsMatch(bare[..bare.IndexOf("enum", StringComparison.Ordinal)], @"\bpublic\b"))
        {
            yield break;
        }

        int line = Locate(lines, enumDeclaration.Line, name);
        yield return (
            line,
            SourcePreChecks.EnumOnlyFile,
            $"File holds only the non-public enum '{name}'; nest it in its owner unless several types share it.",
            lines[line - 1].Raw.Trim());
    }

    /// <summary>
    /// Declaration text starts at its first attribute; this finds the line that names the member.
    /// </summary>
    private static int Locate(IReadOnlyList<CSharpSourceScanner.ScannedLine> lines, int startLine, string name)
    {
        var pattern = new Regex($@"\b{Regex.Escape(name)}\b");
        int last = Math.Min(lines.Count, startLine + 12);

        for (int number = startLine; number <= last; number++)
        {
            string code = CSharpMemberScanner.WithoutAttributes(lines[number - 1].Code);
            if (pattern.IsMatch(code))
            {
                return number;
            }
        }

        return startLine;
    }
}
