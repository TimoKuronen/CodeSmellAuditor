using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeSmellAuditor.Fix;

/// <summary>
/// Removes #region and #endregion lines whose name is not one of the fixed structural names.
/// Only the directive lines go; the code between them is untouched. A region without a matching
/// #endregion is reported as skipped instead of changed.
/// </summary>
public sealed class RegionFixer : IStyleFixer
{
    public const string Rule = "region-block";

    private static readonly Regex RegionLine = new(@"^\s*#\s*region\b(?<name>.*)$", RegexOptions.Compiled);

    private static readonly HashSet<string> StructuredNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Serialized Fields", "Lifecycle", "Public API", "Private Helpers"
    };

    public string RuleId => Rule;

    public Task<FixerOutput> FixAsync(FixInput input, CancellationToken cancellationToken)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(input.Content, cancellationToken: cancellationToken);
        SourceText text = tree.GetText(cancellationToken);
        var removed = new HashSet<int>();
        var changes = new List<FixChange>();
        var skips = new List<FixSkip>();

        foreach (SyntaxTrivia trivia in tree.GetRoot(cancellationToken).DescendantTrivia(descendIntoTrivia: true).Where(t => t.IsKind(SyntaxKind.RegionDirectiveTrivia)))
        {
            var directive = (DirectiveTriviaSyntax)trivia.GetStructure()!;
            int startLine = text.Lines.GetLineFromPosition(directive.SpanStart).LineNumber;
            Match match = RegionLine.Match(text.Lines[startLine].ToString());
            string name = match.Success ? match.Groups["name"].Value.Trim() : string.Empty;
            if (StructuredNames.Contains(name))
            {
                continue;
            }

            var end = directive.GetRelatedDirectives().FirstOrDefault(d => d.IsKind(SyntaxKind.EndRegionDirectiveTrivia));
            if (end is null)
            {
                skips.Add(new FixSkip(Rule, startLine + 1, name, "the region has no matching #endregion"));
                continue;
            }

            removed.Add(startLine);
            removed.Add(text.Lines.GetLineFromPosition(end.SpanStart).LineNumber);
            changes.Add(new FixChange(Rule, startLine + 1, name.Length == 0 ? "removed #region and its #endregion" : $"removed #region {name} and its #endregion"));
        }

        if (removed.Count == 0)
        {
            return Task.FromResult(FixerOutput.Unchanged(input.Content, skips));
        }

        return Task.FromResult(new FixerOutput(Rebuild(text, removed), changes, skips));
    }

    private static string Rebuild(SourceText text, HashSet<int> removed)
    {
        var builder = new StringBuilder(text.Length);
        bool previousKeptBlank = true;
        bool afterRemoval = false;

        for (int i = 0; i < text.Lines.Count; i++)
        {
            TextLine line = text.Lines[i];
            if (removed.Contains(i))
            {
                afterRemoval = true;
                continue;
            }

            bool blank = string.IsNullOrWhiteSpace(line.ToString());
            if (afterRemoval && blank && previousKeptBlank)
            {
                afterRemoval = false;
                continue;
            }

            afterRemoval = false;
            previousKeptBlank = blank;
            builder.Append(text.ToString(line.SpanIncludingLineBreak));
        }

        return builder.ToString();
    }
}
