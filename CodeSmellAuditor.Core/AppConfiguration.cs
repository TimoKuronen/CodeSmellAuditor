namespace CodeSmellAuditor.Core;

/// <summary>
/// Settings shared by the hosts: storage location, models, context limits and stack.
/// An environment variable wins over the matching caller-supplied value.
/// </summary>
public sealed record AppConfiguration(
    string StoragePath,
    string ModelName,
    string FixModelName,
    int NumCtx,
    int NumPredict,
    string? Stack)
{
    public const string StorageVariable = "CODESMELL_STORAGE";
    public const string ModelVariable = "CODESMELL_MODEL";
    public const string FixModelVariable = "CODESMELL_FIX_MODEL";
    public const string NumCtxVariable = "CODESMELL_NUM_CTX";
    public const string NumPredictVariable = "CODESMELL_NUM_PREDICT";

    private const string StorageFolderName = "WorkstationStorage";

    public string RulesPath => Path.Combine(StoragePath, "Rules");

    public string TargetsPath => Path.Combine(StoragePath, "Targets");

    /// <param name="environment">Returns the value of an environment variable, or null when unset.</param>
    /// <param name="baseDirectory">Folder the storage search starts from when no path is given.</param>
    public static AppConfiguration Resolve(
        string? storage,
        string? model,
        string? fixModel,
        string? stack,
        Func<string, string?> environment,
        string baseDirectory)
    {
        var defaults = new AuditConfiguration();
        string modelName = FirstValue(environment(ModelVariable), model) ?? AuditConfiguration.DefaultModelName;

        return new AppConfiguration(
            ResolveStoragePath(environment(StorageVariable), storage, baseDirectory),
            modelName,
            FirstValue(environment(FixModelVariable), fixModel) ?? modelName,
            int.TryParse(environment(NumCtxVariable), out int numCtx) ? numCtx : defaults.NumCtx,
            int.TryParse(environment(NumPredictVariable), out int numPredict) ? numPredict : defaults.NumPredict,
            stack);
    }

    /// <summary>
    /// Model settings for a single-file audit.
    /// </summary>
    public AuditConfiguration ForFileAudit() =>
        new(ModelName: ModelName, NumCtx: NumCtx, NumPredict: NumPredict);

    /// <summary>
    /// Model settings for a system audit, which has its own fixed budget.
    /// </summary>
    public AuditConfiguration ForSystemAudit() => AuditConfiguration.ForSystemAudit(ModelName);

    private static string? FirstValue(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        return string.IsNullOrWhiteSpace(second) ? null : second;
    }

    private static string ResolveStoragePath(string? fromEnvironment, string? fromCaller, string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        if (!string.IsNullOrWhiteSpace(fromCaller))
        {
            return Path.GetFullPath(fromCaller);
        }

        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, StorageFolderName);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(
            Path.Combine(baseDirectory, "..", "..", "..", "..", StorageFolderName));
    }
}
