using System.Text;

namespace CodeSmellAuditor.Core;

/// <summary>
/// Splits C# source into per-line code and comment text. String and char literals are blanked so
/// their content is never mistaken for code or comments; this is a lexical pass, not a parser.
/// </summary>
internal static class CSharpSourceScanner
{
    internal sealed record ScannedLine(int Number, string Raw, string Code, string Comment, bool IsDocComment);

    private enum State
    {
        Code,
        BlockComment,
        VerbatimString,
        RawString
    }

    public static IReadOnlyList<ScannedLine> Scan(string content)
    {
        string[] rawLines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var lines = new List<ScannedLine>(rawLines.Length);

        State state = State.Code;
        int rawQuoteCount = 0;

        for (int index = 0; index < rawLines.Length; index++)
        {
            string raw = rawLines[index];
            var code = new StringBuilder();
            var comment = new StringBuilder();
            bool isDoc = false;

            int i = 0;
            while (i < raw.Length)
            {
                char c = raw[i];
                char next = i + 1 < raw.Length ? raw[i + 1] : '\0';

                switch (state)
                {
                    case State.BlockComment:
                        if (c == '*' && next == '/')
                        {
                            state = State.Code;
                            i += 2;
                        }
                        else
                        {
                            comment.Append(c);
                            i++;
                        }
                        break;

                    case State.VerbatimString:
                        if (c == '"' && next == '"')
                        {
                            i += 2;
                        }
                        else
                        {
                            if (c == '"')
                            {
                                state = State.Code;
                            }
                            i++;
                        }
                        break;

                    case State.RawString:
                        if (CountQuoteRun(raw, i) >= rawQuoteCount)
                        {
                            state = State.Code;
                            i += CountQuoteRun(raw, i);
                        }
                        else
                        {
                            i++;
                        }
                        break;

                    default:
                        if (c == '/' && next == '/')
                        {
                            isDoc = i + 2 < raw.Length && raw[i + 2] == '/' && !(i + 3 < raw.Length && raw[i + 3] == '/');
                            comment.Append(raw, i + (isDoc ? 3 : 2), raw.Length - i - (isDoc ? 3 : 2));
                            i = raw.Length;
                        }
                        else if (c == '/' && next == '*')
                        {
                            state = State.BlockComment;
                            i += 2;
                        }
                        else if (c == '"')
                        {
                            i = ReadStringStart(raw, i, code, ref state, ref rawQuoteCount);
                        }
                        else if (c == '\'')
                        {
                            i = SkipCharLiteral(raw, i, code);
                        }
                        else
                        {
                            code.Append(c);
                            i++;
                        }
                        break;
                }
            }

            lines.Add(new ScannedLine(index + 1, raw, code.ToString(), comment.ToString(), isDoc));
        }

        return lines;
    }

    private static int ReadStringStart(string raw, int start, StringBuilder code, ref State state, ref int rawQuoteCount)
    {
        int quoteRun = CountQuoteRun(raw, start);
        if (quoteRun >= 3)
        {
            state = State.RawString;
            rawQuoteCount = quoteRun;
            code.Append("\"\"");
            return start + quoteRun;
        }

        bool verbatim = EndsWithVerbatimPrefix(code);
        code.Append("\"\"");

        int i = start + 1;
        if (verbatim)
        {
            state = State.VerbatimString;
            return i;
        }

        while (i < raw.Length)
        {
            if (raw[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (raw[i] == '"')
            {
                return i + 1;
            }

            i++;
        }

        return raw.Length;
    }

    private static bool EndsWithVerbatimPrefix(StringBuilder code)
    {
        int length = code.Length;
        if (length >= 1 && code[length - 1] == '@')
        {
            return true;
        }

        return length >= 2 && code[length - 1] == '$' && code[length - 2] == '@';
    }

    private static int SkipCharLiteral(string raw, int start, StringBuilder code)
    {
        int i = start + 1;
        if (i < raw.Length && raw[i] == '\\')
        {
            int close = raw.IndexOf('\'', i + 2);
            i = close < 0 ? raw.Length : close;
        }
        else
        {
            i++;
        }

        if (i < raw.Length && raw[i] == '\'')
        {
            code.Append("' '");
            return i + 1;
        }

        code.Append('\'');
        return start + 1;
    }

    private static int CountQuoteRun(string raw, int start)
    {
        int count = 0;
        while (start + count < raw.Length && raw[start + count] == '"')
        {
            count++;
        }

        return count;
    }
}
