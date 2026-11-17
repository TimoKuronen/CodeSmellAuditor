using System.Diagnostics;

namespace CodeSmellAuditor.Eval;

/// <summary>
/// Reads the short commit sha and whether the working tree has uncommitted changes.
/// </summary>
public static class GitInfo
{
    public static (string Sha, bool Dirty) Read(string repositoryRoot)
    {
        string sha = Run(repositoryRoot, "rev-parse --short HEAD").Trim();
        string status = Run(repositoryRoot, "status --porcelain");
        return (string.IsNullOrEmpty(sha) ? "unknown" : sha, !string.IsNullOrWhiteSpace(status));
    }

    private static string Run(string workingDirectory, string arguments)
    {
        try
        {
            var info = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using Process process = Process.Start(info)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }
}
