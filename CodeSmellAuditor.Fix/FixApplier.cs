namespace CodeSmellAuditor.Fix;

/// <summary>
/// Writes planned fixes to disk. A file is only written when it is unchanged since it was read, using the
/// encoding and line endings it already had. Outside a git checkout a .bak copy is kept next to it.
/// </summary>
public static class FixApplier
{
    public static ApplyResult Apply(FileFix fix)
    {
        string path = fix.Source.Path;

        if (!fix.HasChanges)
        {
            return new ApplyResult(path, false, fix.Error ?? "nothing to change");
        }

        if (LoadedSource.HashOf(path) != fix.Source.Hash)
        {
            return new ApplyResult(path, false, "file changed on disk since it was read");
        }

        if (!IsInsideGitCheckout(path))
        {
            File.Copy(path, path + ".bak", overwrite: true);
        }

        File.WriteAllText(path, fix.FixedContent, fix.Source.Encoding);
        return new ApplyResult(path, true);
    }

    private static bool IsInsideGitCheckout(string path)
    {
        DirectoryInfo? directory = new FileInfo(path).Directory;
        while (directory is not null)
        {
            string marker = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return true;
            }

            directory = directory.Parent;
        }

        return false;
    }
}
