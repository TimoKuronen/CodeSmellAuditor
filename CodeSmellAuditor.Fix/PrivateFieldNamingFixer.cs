using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace CodeSmellAuditor.Fix;

/// <summary>
/// Renames private instance fields that are not camelCase, updating every reference in the file.
/// Fields whose name may be a serialization contract, partial types, and names that would collide
/// or become keywords are reported as skipped instead of changed.
/// </summary>
public sealed class PrivateFieldNamingFixer : IStyleFixer
{
    public const string Rule = "private-field-naming";

    private static readonly HashSet<string> NameNeutralAttributes = new(StringComparer.Ordinal)
    {
        "Inject", "NonSerialized", "HideInInspector", "Obsolete", "SuppressMessage"
    };

    public string RuleId => Rule;

    public async Task<FixerOutput> FixAsync(FixInput input, CancellationToken cancellationToken)
    {
        using SemanticFile file = SemanticFile.Create(input.Path, input.Content);
        Document document = file.Document;
        var changes = new List<FixChange>();
        var skips = new List<FixSkip>();
        var handled = new HashSet<string>(StringComparer.Ordinal);

        while (await FindNextAsync(document, handled, skips, cancellationToken) is { } target)
        {
            Solution renamed = await Renamer.RenameSymbolAsync(
                document.Project.Solution,
                target.Symbol,
                new SymbolRenameOptions(),
                target.NewName,
                cancellationToken);

            document = renamed.GetDocument(document.Id)!;
            changes.Add(new FixChange(Rule, target.Line, $"Rename field '{target.Symbol.Name}' to '{target.NewName}'."));
        }

        if (changes.Count == 0)
        {
            return FixerOutput.Unchanged(input.Content, skips);
        }

        string fixedContent = (await document.GetTextAsync(cancellationToken)).ToString();
        return new FixerOutput(fixedContent, changes, skips);
    }

    private sealed record Target(IFieldSymbol Symbol, string NewName, int Line);

    private static async Task<Target?> FindNextAsync(
        Document document,
        HashSet<string> handled,
        List<FixSkip> skips,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken);
        SemanticModel? model = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || model is null)
        {
            return null;
        }

        foreach (VariableDeclaratorSyntax declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declarator.Parent?.Parent is not FieldDeclarationSyntax declaration
                || model.GetDeclaredSymbol(declarator, cancellationToken) is not IFieldSymbol field
                || !IsPrivateInstanceField(field)
                || !FieldNameConvention.IsDisallowed(field.Name))
            {
                continue;
            }

            string key = $"{field.ContainingType.ToDisplayString()}::{field.Name}";
            if (!handled.Add(key))
            {
                continue;
            }

            int line = declarator.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            string newName = FieldNameConvention.ToCamelCase(field.Name);
            string? reason = SkipReason(field, declaration, newName);
            if (reason is not null)
            {
                skips.Add(new FixSkip(Rule, line, field.Name, reason));
                continue;
            }

            return new Target(field, newName, line);
        }

        return null;
    }

    private static bool IsPrivateInstanceField(IFieldSymbol field) =>
        field.DeclaredAccessibility == Accessibility.Private
        && !field.IsStatic
        && !field.IsConst
        && !field.IsImplicitlyDeclared;

    private static string? SkipReason(IFieldSymbol field, FieldDeclarationSyntax declaration, string newName)
    {
        string? attribute = declaration.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attr => AttributeName(attr))
            .FirstOrDefault(name => !NameNeutralAttributes.Contains(name));
        if (attribute is not null)
        {
            return $"has [{attribute}], so the field name may be a serialization contract";
        }

        if (field.ContainingType.DeclaringSyntaxReferences.Length > 1)
        {
            return "declared in a partial type, so references in other files cannot be updated";
        }

        if (newName == field.Name)
        {
            return "no camelCase form exists";
        }

        if (!SyntaxFacts.IsValidIdentifier(newName) || SyntaxFacts.GetKeywordKind(newName) != SyntaxKind.None)
        {
            return $"'{newName}' is not a usable identifier";
        }

        if (field.ContainingType.GetMembers(newName).Length > 0)
        {
            return $"'{newName}' already exists in {field.ContainingType.Name}";
        }

        return null;
    }

    private static string AttributeName(AttributeSyntax attribute)
    {
        string name = attribute.Name.ToString();
        int dot = name.LastIndexOf('.');
        if (dot >= 0)
        {
            name = name[(dot + 1)..];
        }

        return name.EndsWith("Attribute", StringComparison.Ordinal) && name.Length > "Attribute".Length
            ? name[..^"Attribute".Length]
            : name;
    }
}
