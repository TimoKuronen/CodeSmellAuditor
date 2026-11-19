namespace CodeSmellAuditor.Fix;

/// <summary>
/// Line-based diff between two versions of a file, trimmed to the changed lines plus a few lines of context.
/// </summary>
public static class LineDiff
{
    private const int MaxLcsCells = 4_000_000;

    public static IReadOnlyList<DiffLine> Compute(string before, string after, int context = 2)
    {
        string[] a = SplitLines(before);
        string[] b = SplitLines(after);

        int prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < a.Length - prefix && suffix < b.Length - prefix && a[^(suffix + 1)] == b[^(suffix + 1)])
        {
            suffix++;
        }

        var all = new List<DiffLine>();
        for (int i = 0; i < prefix; i++)
        {
            all.Add(new DiffLine(DiffKind.Context, i + 1, i + 1, a[i]));
        }

        all.AddRange(DiffMiddle(a, b, prefix, a.Length - suffix, b.Length - suffix));

        for (int i = 0; i < suffix; i++)
        {
            int oldIndex = a.Length - suffix + i;
            int newIndex = b.Length - suffix + i;
            all.Add(new DiffLine(DiffKind.Context, oldIndex + 1, newIndex + 1, a[oldIndex]));
        }

        return TrimToContext(all, context);
    }

    private static string[] SplitLines(string text) =>
        text.Length == 0 ? Array.Empty<string>() : text.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

    private static IEnumerable<DiffLine> DiffMiddle(string[] a, string[] b, int start, int aEnd, int bEnd)
    {
        int n = aEnd - start;
        int m = bEnd - start;

        if ((long)n * m > MaxLcsCells)
        {
            return a.Skip(start).Take(n).Select((t, i) => new DiffLine(DiffKind.Removed, start + i + 1, null, t))
                .Concat(b.Skip(start).Take(m).Select((t, i) => new DiffLine(DiffKind.Added, null, start + i + 1, t)));
        }

        var table = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                table[i, j] = a[start + i] == b[start + j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        var result = new List<DiffLine>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[start + x] == b[start + y])
            {
                result.Add(new DiffLine(DiffKind.Context, start + x + 1, start + y + 1, a[start + x]));
                x++;
                y++;
            }
            else if (table[x + 1, y] >= table[x, y + 1])
            {
                result.Add(new DiffLine(DiffKind.Removed, start + x + 1, null, a[start + x]));
                x++;
            }
            else
            {
                result.Add(new DiffLine(DiffKind.Added, null, start + y + 1, b[start + y]));
                y++;
            }
        }

        for (; x < n; x++)
        {
            result.Add(new DiffLine(DiffKind.Removed, start + x + 1, null, a[start + x]));
        }

        for (; y < m; y++)
        {
            result.Add(new DiffLine(DiffKind.Added, null, start + y + 1, b[start + y]));
        }

        return result;
    }

    private static IReadOnlyList<DiffLine> TrimToContext(List<DiffLine> all, int context)
    {
        var keep = new bool[all.Count];
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Kind == DiffKind.Context)
            {
                continue;
            }

            for (int k = Math.Max(0, i - context); k <= Math.Min(all.Count - 1, i + context); k++)
            {
                keep[k] = true;
            }
        }

        var trimmed = new List<DiffLine>();
        bool gapPending = false;
        for (int i = 0; i < all.Count; i++)
        {
            if (!keep[i])
            {
                gapPending = trimmed.Count > 0;
                continue;
            }

            if (gapPending)
            {
                trimmed.Add(new DiffLine(DiffKind.Gap, null, null, string.Empty));
                gapPending = false;
            }

            trimmed.Add(all[i]);
        }

        return trimmed;
    }
}
