using ClaudeOS.Core.Safety;
using System.Text;

namespace ClaudeOS.Core.Tests.Safety;

[Collection("serial")]
public sealed class OverlayTests : IDisposable
{
    private readonly TestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Paths_cannot_escape_the_workspace()
    {
        var overlay = _ws.Overlay();
        var outside = Path.Combine(_ws.Base, "outside.txt");
        File.WriteAllText(outside, "x");
        File.CreateSymbolicLink(_ws.P("link.txt"), outside);

        foreach (var path in new[] { "../outside.txt", outside, "link.txt", "invoices/../../outside.txt" })
        {
            Assert.Throws<PathEscapeException>(() => overlay.Rel(path));
        }

        Assert.Equal("invoices/a.txt", overlay.Rel(_ws.P("invoices/a.txt")));
        Assert.Equal(".", overlay.Rel("."));
    }

    [Fact]
    public void A_symlinked_directory_cannot_smuggle_writes_outside()
    {
        var outsideDir = Path.Combine(_ws.Base, "elsewhere");
        Directory.CreateDirectory(outsideDir);
        Directory.CreateSymbolicLink(_ws.P("shortcut"), outsideDir);
        var overlay = _ws.Overlay();
        Assert.Throws<PathEscapeException>(() => overlay.Rel("shortcut/new.txt"));
    }

    [Fact]
    public void Staged_changes_are_visible_but_not_on_disk()
    {
        var overlay = _ws.Overlay();
        overlay.Write("reports/summary.md", B("total 200.50\n"));
        overlay.Delete("notes.md");
        overlay.Move("invoices/b.txt", "archive/b.txt");

        Assert.Equal(B("total 200.50\n"), overlay.Read("reports/summary.md"));
        Assert.Null(overlay.Read("notes.md"));
        Assert.Equal([".env", ".ssh/", "archive/", "invoices/", "reports/"], overlay.ListDir("."));
        Assert.Equal(["a.txt"], overlay.ListDir("invoices"));
        Assert.False(_ws.Exists("reports"));
        Assert.True(_ws.Exists("notes.md"));

        var diff = overlay.Diff();
        Assert.Contains("+++ b/reports/summary.md", diff);
        Assert.Contains("--- a/notes.md", diff);
        Assert.Contains("+++ /dev/null", diff);
    }

    [Fact]
    public void Commit_then_undo_restores_everything()
    {
        var overlay = _ws.Overlay();
        overlay.Write("notes.md", B("new notes\n"));
        overlay.Write("reports/summary.md", B("summary\n"));
        overlay.Delete("invoices/a.txt");
        var manifest = overlay.Commit(_ws.State.Journal, "c1");

        Assert.Equal("new notes\n", _ws.ReadText("notes.md"));
        Assert.True(_ws.Exists("reports/summary.md"));
        Assert.False(_ws.Exists("invoices/a.txt"));

        Overlay.Undo(manifest);
        Assert.Equal("old notes\n", _ws.ReadText("notes.md"));
        Assert.False(_ws.Exists("reports")); // directories the commit created go too
        Assert.True(Directory.Exists(_ws.P("invoices"))); // pre-existing directories are left alone
        Assert.Equal("Acme Corp\nTotal: 120.00\n", _ws.ReadText("invoices/a.txt"));
        var again = Assert.Throws<ConflictException>(() => Overlay.Undo(manifest));
        Assert.Contains("already undone", again.Message);
    }

    [Fact]
    public void Commit_refuses_if_file_changed_after_preview()
    {
        var overlay = _ws.Overlay();
        overlay.Write("notes.md", B("planned\n"));
        File.WriteAllText(_ws.P("notes.md"), "edited by the person meanwhile\n");
        var e = Assert.Throws<ConflictException>(() => overlay.Commit(_ws.State.Journal, "c1"));
        Assert.Contains("notes.md", e.Message);
        Assert.Equal("edited by the person meanwhile\n", _ws.ReadText("notes.md"));
    }

    [Fact]
    public void Undo_refuses_if_file_edited_after_commit()
    {
        var overlay = _ws.Overlay();
        overlay.Write("notes.md", B("planned\n"));
        var manifest = overlay.Commit(_ws.State.Journal, "c1");
        File.WriteAllText(_ws.P("notes.md"), "later edit\n");
        var e = Assert.Throws<ConflictException>(() => Overlay.Undo(manifest));
        Assert.Contains("notes.md", e.Message);
        Assert.Equal("later edit\n", _ws.ReadText("notes.md"));
    }

    [Fact]
    public void A_failed_commit_can_still_be_undone()
    {
        var overlay = _ws.Overlay();
        overlay.Write("invoices/a.txt", B("changed\n"));
        overlay.Write("notes.md", B("changed\n"));
        var real = Overlay.AtomicWriter;
        try
        {
            Overlay.AtomicWriter = (target, data) =>
            {
                if (Path.GetFileName(target) == "notes.md")
                {
                    throw new IOException("disk full");
                }

                real(target, data);
            };
            var e = Assert.Throws<CommitException>(() => overlay.Commit(_ws.State.Journal, "c1"));
            Assert.Contains("part-way", e.Message);
        }
        finally
        {
            Overlay.AtomicWriter = real;
        }

        Assert.Equal("changed\n", _ws.ReadText("invoices/a.txt")); // first change landed
        Overlay.Undo(Path.Combine(_ws.State.Journal, "c1", "manifest.json"));
        Assert.Equal("Acme Corp\nTotal: 120.00\n", _ws.ReadText("invoices/a.txt"));
        Assert.Equal("old notes\n", _ws.ReadText("notes.md"));
    }

    [Fact]
    public void A_file_cannot_become_a_directory()
    {
        var overlay = _ws.Overlay();
        Assert.Throws<NotADirectoryException>(() => overlay.Write("notes.md/inside.txt", B("x")));
        Assert.Throws<IsADirectoryException>(() => overlay.Write("invoices", B("x")));
    }

    [Fact]
    public void Writing_identical_content_is_not_a_change()
    {
        var overlay = _ws.Overlay();
        overlay.Write("notes.md", B("old notes\n"));
        Assert.Empty(overlay.Changes());
    }

    [Fact]
    public void Diff_is_a_real_unified_diff_with_hunks()
    {
        File.WriteAllText(_ws.P("long.txt"), string.Join("\n", Enumerable.Range(1, 40).Select(i => $"line {i}")) + "\n");
        var overlay = _ws.Overlay();
        var lines = Enumerable.Range(1, 40).Select(i => i == 20 ? "line twenty" : $"line {i}");
        overlay.Write("long.txt", B(string.Join("\n", lines) + "\n"));

        var diff = overlay.Diff();
        Assert.Contains("@@ -17,7 +17,7 @@", diff);
        Assert.Contains("-line 20\n+line twenty\n", diff);
        Assert.DoesNotContain("line 1\n", diff);
    }

    [Fact]
    public void Binary_files_are_summarised_not_dumped()
    {
        var overlay = _ws.Overlay();
        overlay.Write("blob.bin", [0xff, 0xfe, 0x00, 0x01]);
        Assert.Contains("Binary file blob.bin: create", overlay.Diff());
    }
}
