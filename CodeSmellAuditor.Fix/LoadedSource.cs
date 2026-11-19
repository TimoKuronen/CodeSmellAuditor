using System.Security.Cryptography;
using System.Text;

namespace CodeSmellAuditor.Fix;

/// <summary>
/// A file read from disk with the encoding and content hash needed to write it back unchanged
/// apart from the fix.
/// </summary>
public sealed record LoadedSource(string Path, string Content, Encoding Encoding, string Hash)
{
    public static LoadedSource Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        string content = reader.ReadToEnd();
        return new LoadedSource(path, content, reader.CurrentEncoding, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public static string HashOf(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
