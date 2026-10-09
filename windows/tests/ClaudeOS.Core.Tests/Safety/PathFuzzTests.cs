using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests.Safety;

/// <summary>
/// A randomized attack on the one function every action's path goes through. Thousands of paths are
/// built from the fragments attackers actually use (parent hops, drive letters, share prefixes, streams,
/// device names, odd separators, look-alike characters), and every one must be refused or resolve to a
/// real path inside the workspace that touches nothing protected. The seed is fixed so a failure repeats.
/// </summary>
public sealed class PathFuzzTests : IDisposable
{
    private static readonly string[] Fragments =
    [
        "..", ".", "...", "/", "\\", "//", "\\\\", "a", "reports", "ws", "invoices", "notes.md", ":", "C:", "c:\\", "\\\\?\\", "%2e%2e", "%2f",
        " ", "  ", ".git", ".ssh", ".env", ".ENV", "id_rsa", "NUL", "con", "COM1", "~", "$", "\u202e", "\u2024", "\uFF0E\uFF0E", "\u0000", "\t", "*", "?", "<", ">", "|", "\"",
        "a.txt", "b.txt:stream", "file.", "file ", new string('x', 300),
    ];

    private readonly TestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void No_path_an_attacker_can_build_escapes_the_workspace_or_reaches_a_protected_name()
    {
        var policy = new Policy();
        var overlay = _ws.Overlay();
        var root = Path.GetFullPath(overlay.Root).TrimEnd(Path.DirectorySeparatorChar, '/') + Path.DirectorySeparatorChar;
        var random = new Random(20261009);
        var allowed = 0;

        for (var i = 0; i < 20_000; i++)
        {
            var path = string.Concat(Enumerable.Range(0, random.Next(1, 7)).Select(_ => Fragments[random.Next(Fragments.Length)]));
            string rel;
            try
            {
                rel = policy.CheckPath(overlay, path);
            }
            catch (PolicyDeniedException)
            {
                continue; // refused: fine
            }

            allowed++;
            Assert.DoesNotContain("..", rel.Split('/'));
            Assert.False(Path.IsPathRooted(rel), $"'{path}' resolved to a rooted path '{rel}'");
            foreach (var part in rel.Split('/'))
            {
                Assert.False(policy.IsProtectedName(part) && part != ".", $"'{path}' reached the protected name '{part}'");
                Assert.DoesNotContain(':', part);
            }

            var full = Path.GetFullPath(Path.Combine(overlay.Root, rel.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(full.StartsWith(root, StringComparison.Ordinal) || full == root.TrimEnd(Path.DirectorySeparatorChar), $"'{path}' resolved outside the workspace: {full}");
        }

        Assert.True(allowed > 0, "the generator should also produce some harmless paths, or this proves nothing");
    }
}
