using CodeSmellAuditor.Fix;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class LineDiffTests
{
    [Fact]
    public void IdenticalText_HasNoDiffLines()
    {
        Assert.Empty(LineDiff.Compute("a\nb\nc", "a\nb\nc"));
    }

    [Fact]
    public void ChangedLine_ShowsRemovedAndAddedWithContext()
    {
        string before = "one\ntwo\nthree\nfour\nfive\nsix\nseven";
        string after = "one\ntwo\nthree\nFOUR\nfive\nsix\nseven";

        IReadOnlyList<DiffLine> diff = LineDiff.Compute(before, after, context: 1);

        Assert.Equal(
            new[] { DiffKind.Context, DiffKind.Removed, DiffKind.Added, DiffKind.Context },
            diff.Select(d => d.Kind));
        DiffLine removed = diff.Single(d => d.Kind == DiffKind.Removed);
        Assert.Equal(4, removed.OldLine);
        Assert.Equal("four", removed.Text);
        Assert.Equal(4, diff.Single(d => d.Kind == DiffKind.Added).NewLine);
    }

    [Fact]
    public void DistantChanges_AreSeparatedByAGap()
    {
        string before = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"line{i}"));
        string after = before.Replace("line2", "LINE2").Replace("line19", "LINE19");

        IReadOnlyList<DiffLine> diff = LineDiff.Compute(before, after, context: 1);

        Assert.Single(diff, d => d.Kind == DiffKind.Gap);
    }

    [Fact]
    public void InsertedAndDeletedLines_KeepTheirOwnLineNumbers()
    {
        IReadOnlyList<DiffLine> diff = LineDiff.Compute("a\nb\nc", "a\nx\ny\nc", context: 0);

        Assert.Equal(new[] { "b" }, diff.Where(d => d.Kind == DiffKind.Removed).Select(d => d.Text));
        Assert.Equal(new[] { 2, 3 }, diff.Where(d => d.Kind == DiffKind.Added).Select(d => d.NewLine!.Value));
    }

    [Fact]
    public void LineEndings_DoNotCreateChanges()
    {
        Assert.Empty(LineDiff.Compute("a\r\nb\r\n", "a\nb\n"));
    }
}
