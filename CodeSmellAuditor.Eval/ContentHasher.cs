using System.Security.Cryptography;
using System.Text;

namespace CodeSmellAuditor.Eval;

/// <summary>
/// Short content hash over a set of files, stable across line-ending and BOM differences.
/// </summary>
public static class ContentHasher
{
    public static string Hash(string baseDirectory, IEnumerable<string> files)
    {
        using var sha = SHA256.Create();
        foreach (string file in files.OrderBy(f => f, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(baseDirectory, file).Replace('\\', '/');
            string content = File.ReadAllText(file).Replace("\r\n", "\n");
            byte[] bytes = Encoding.UTF8.GetBytes(relative + "\n" + content + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!)[..8].ToLowerInvariant();
    }

    public static string HashDirectory(string directory, string searchPattern, SearchOption option = SearchOption.TopDirectoryOnly) =>
        Hash(directory, Directory.Exists(directory) ? Directory.GetFiles(directory, searchPattern, option) : Array.Empty<string>());
}
