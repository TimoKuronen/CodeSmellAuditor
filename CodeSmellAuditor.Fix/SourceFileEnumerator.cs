namespace CodeSmellAuditor.Fix;

/// <summary>
/// Expands file and folder arguments into the .cs files a fix run covers.
/// </summary>
public static class SourceFileEnumerator
{
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj", "bin", "Library", "Temp", "Packages", ".git", ".vs", ".idea", "node_modules"
    };

    private static readonly string[] GeneratedSuffixes = { ".g.cs", ".generated.cs", ".Designer.cs", ".AssemblyInfo.cs" };

    public static IReadOnlyList<string> Expand(IEnumerable<string> paths)
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string raw in paths)
        {
            string path = Path.GetFullPath(raw);

            if (File.Exists(path))
            {
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Not a .cs file: {raw}");
                }

                files.Add(path);
            }
            else if (Directory.Exists(path))
            {
                Collect(path, files);
            }
            else
            {
                throw new FileNotFoundException($"Path not found: {raw}", path);
            }
        }

        return files.ToList();
    }

    private static void Collect(string folder, SortedSet<string> files)
    {
        foreach (string file in Directory.EnumerateFiles(folder, "*.cs"))
        {
            if (!GeneratedSuffixes.Any(suffix => file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                files.Add(file);
            }
        }

        foreach (string child in Directory.EnumerateDirectories(folder))
        {
            if (!SkippedFolders.Contains(Path.GetFileName(child)))
            {
                Collect(child, files);
            }
        }
    }
}
