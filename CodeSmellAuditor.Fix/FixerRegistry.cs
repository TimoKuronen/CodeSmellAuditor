namespace CodeSmellAuditor.Fix;

/// <summary>
/// The fixers a normal run uses, one per mechanical rule.
/// </summary>
public static class FixerRegistry
{
    public static IReadOnlyList<IStyleFixer> CreateDefault() => new IStyleFixer[]
    {
        new PrivateFieldNamingFixer(),
        new RegionFixer()
    };
}
