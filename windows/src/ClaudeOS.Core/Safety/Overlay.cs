using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeOS.Core.Safety;

/// <summary>A path resolves outside the workspace root.</summary>
public sealed class PathEscapeException(string message) : Exception(message);

/// <summary>Files on disk changed after they were staged or committed.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>A commit failed part-way; its journal can still undo what was applied.</summary>
public sealed class CommitException(string message, Exception inner) : Exception(message, inner);

public sealed class IsADirectoryException(string message) : IOException(message);

public sealed class NotADirectoryException(string message) : IOException(message);

public enum ChangeKind { Create, Modify, Delete }

public sealed record Change(string Path, byte[]? Before, byte[]? After)
{
    public ChangeKind Kind => Before is null ? ChangeKind.Create : After is null ? ChangeKind.Delete : ChangeKind.Modify;
}

public sealed class JournalEntry
{
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("backup")] public string? Backup { get; set; }
    [JsonPropertyName("before_sha256")] public string? BeforeSha256 { get; set; }
    [JsonPropertyName("after_sha256")] public string? AfterSha256 { get; set; }
}

public sealed class JournalManifest
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("root")] public string Root { get; set; } = "";
    [JsonPropertyName("entries")] public List<JournalEntry> Entries { get; set; } = [];
    [JsonPropertyName("created_dirs")] public List<string> CreatedDirs { get; set; } = [];
    [JsonPropertyName("undone"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Undone { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(JournalManifest))]
internal sealed partial class JournalJson : JsonSerializerContext;

/// <summary>
/// Copy-on-write overlay over one workspace directory.
///
/// Local actions are staged here in memory, so they can be previewed as a diff before
/// anyone approves them. Committing writes an undo journal first, then applies the changes.
/// This covers file edits only; running commands needs a real isolation backend.
/// </summary>
public sealed class Overlay
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly Dictionary<string, byte[]?> _staged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]?> _base = new(StringComparer.Ordinal);

    public Overlay(string root)
    {
        var full = Path.GetFullPath(root);
        if (!Directory.Exists(full))
        {
            throw new DirectoryNotFoundException($"workspace root {full} is not a directory");
        }

        Root = RealPath(full);
    }

    /// <summary>The workspace root with every symbolic link resolved.</summary>
    public string Root { get; }

    /// <summary>A fresh overlay over the same root with nothing staged (used for dry runs).</summary>
    public Overlay Fork() => new(Root);

    /// <summary>
    /// Normalize a path to workspace-relative POSIX form, refusing escapes. Resolution
    /// follows symbolic links, so a link inside the workspace that points outside it is
    /// refused too.
    /// </summary>
    public string Rel(string path)
    {
        var combined = Path.IsPathRooted(path) ? path : Path.Combine(Root, path);
        var resolved = RealPath(combined);
        if (string.Equals(resolved, Root, PathComparison))
        {
            return ".";
        }

        var prefix = Root.EndsWith(Path.DirectorySeparatorChar) ? Root : Root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, PathComparison))
        {
            throw new PathEscapeException($"{path} is outside the workspace {Root}");
        }

        return resolved[prefix.Length..].Replace(Path.DirectorySeparatorChar, '/');
    }

    private string Abs(string rel) => rel == "." ? Root : Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));

    private byte[]? Disk(string rel)
    {
        var p = Abs(rel);
        if (File.Exists(p))
        {
            return File.ReadAllBytes(p);
        }

        if (Directory.Exists(p))
        {
            throw new IsADirectoryException($"{rel} is a directory");
        }

        return null;
    }

    public byte[]? Read(string path)
    {
        var rel = Rel(path);
        return _staged.TryGetValue(rel, out var staged) ? staged : Disk(rel);
    }

    public bool IsDir(string path)
    {
        var rel = Rel(path);
        if (rel == "." || Directory.Exists(Abs(rel)))
        {
            return true;
        }

        var prefix = rel + "/";
        return _staged.Any(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal) && kv.Value is not null);
    }

    /// <summary>Entries as the directory will look once staged changes apply; directories end in '/'.</summary>
    public IReadOnlyList<string> ListDir(string path = ".")
    {
        var rel = Rel(path);
        var dir = Abs(rel);
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (Directory.Exists(dir))
        {
            foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos())
            {
                names.Add(entry.Name + (entry.Attributes.HasFlag(FileAttributes.Directory) ? "/" : ""));
            }
        }

        var prefix = rel == "." ? "" : rel + "/";
        foreach (var (staged, content) in _staged)
        {
            if (!staged.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var tail = staged[prefix.Length..];
            var slash = tail.IndexOf('/');
            if (slash >= 0)
            {
                if (content is not null)
                {
                    names.Add(tail[..slash] + "/");
                }
            }
            else if (content is null)
            {
                names.Remove(tail);
            }
            else
            {
                names.Add(tail);
            }
        }

        return [.. names.Order(StringComparer.Ordinal)];
    }

    private bool IsFile(string rel) => _staged.TryGetValue(rel, out var v) ? v is not null : File.Exists(Abs(rel));

    private void Touch(string rel)
    {
        if (!_base.ContainsKey(rel))
        {
            _base[rel] = Disk(rel);
        }
    }

    public void Write(string path, byte[] data)
    {
        var rel = Rel(path);
        if (IsDir(rel))
        {
            throw new IsADirectoryException($"{path} is a directory");
        }

        var parent = ParentOf(rel);
        while (parent.Length > 0)
        {
            if (IsFile(parent))
            {
                throw new NotADirectoryException($"{parent} is a file, so {rel} cannot be created");
            }

            parent = ParentOf(parent);
        }

        Touch(rel);
        _staged[rel] = data;
    }

    public void Delete(string path)
    {
        var rel = Rel(path);
        if (Read(rel) is null)
        {
            throw new FileNotFoundException($"{path} does not exist");
        }

        Touch(rel);
        _staged[rel] = null;
    }

    public void Move(string src, string dst)
    {
        var data = Read(src) ?? throw new FileNotFoundException($"{src} does not exist");
        Write(dst, data);
        Delete(src);
    }

    private static string ParentOf(string rel)
    {
        var slash = rel.LastIndexOf('/');
        return slash < 0 ? "" : rel[..slash];
    }

    public IReadOnlyList<Change> Changes() =>
    [
        .. _staged.Keys.Order(StringComparer.Ordinal)
            .Select(rel => new Change(rel, _base[rel], _staged[rel]))
            .Where(c => !Same(c.Before, c.After)),
    ];

    private static bool Same(byte[]? a, byte[]? b) => a is null || b is null ? a is null && b is null : a.AsSpan().SequenceEqual(b);

    public string Diff()
    {
        var sb = new StringBuilder();
        foreach (var c in Changes())
        {
            if (!TryDecode(c.Before, out var before) || !TryDecode(c.After, out var after))
            {
                sb.Append($"Binary file {c.Path}: {c.Kind.ToString().ToLowerInvariant()}\n");
                continue;
            }

            var from = c.Before is null ? "/dev/null" : $"a/{c.Path}";
            var to = c.After is null ? "/dev/null" : $"b/{c.Path}";
            var diff = UnifiedDiff.Generate(UnifiedDiff.SplitLines(before), UnifiedDiff.SplitLines(after), from, to);
            sb.Append(diff.Length > 0 ? diff : $"--- {from}\n+++ {to}\n(empty file)\n");
        }

        return sb.ToString();
    }

    private static bool TryDecode(byte[]? data, out string text)
    {
        text = "";
        if (data is null)
        {
            return true;
        }

        try
        {
            text = new UTF8Encoding(false, true).GetString(data);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// Apply staged changes and return the undo manifest path. Refuses if any touched file
    /// changed on disk since it was staged, so an approval never lands on content the person
    /// did not see.
    /// </summary>
    public string Commit(string journalDir, string commitId)
    {
        var changes = Changes();
        var stale = changes.Where(c => !Same(Disk(c.Path), c.Before)).Select(c => c.Path).ToList();
        if (stale.Count > 0)
        {
            throw new ConflictException($"changed on disk since preview: {string.Join(", ", stale)}");
        }

        var backupDir = Path.Combine(journalDir, commitId);
        if (Directory.Exists(backupDir))
        {
            throw new IOException($"journal entry {commitId} already exists");
        }

        Directory.CreateDirectory(backupDir);
        var manifest = new JournalManifest { Id = commitId, Root = Root };
        for (var i = 0; i < changes.Count; i++)
        {
            var c = changes[i];
            string? backup = null;
            if (c.Before is not null)
            {
                backup = $"{i}.bak";
                File.WriteAllBytes(Path.Combine(backupDir, backup), c.Before);
            }

            manifest.Entries.Add(new JournalEntry { Path = c.Path, Backup = backup, BeforeSha256 = Sha(c.Before), AfterSha256 = Sha(c.After) });
        }

        var created = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var c in changes.Where(c => c.After is not null))
        {
            for (var parent = ParentOf(c.Path); parent.Length > 0; parent = ParentOf(parent))
            {
                if (!Directory.Exists(Abs(parent)))
                {
                    created.Add(parent);
                }
            }
        }

        manifest.CreatedDirs = [.. created];
        var manifestPath = Path.Combine(backupDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JournalJson.Default.JournalManifest));

        try
        {
            foreach (var c in changes)
            {
                var target = Abs(c.Path);
                if (c.After is null)
                {
                    File.Delete(target);
                }
                else
                {
                    AtomicWrite(target, c.After);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new CommitException($"commit {commitId} failed part-way ({e.Message}); undo it to restore the files", e);
        }

        _staged.Clear();
        _base.Clear();
        return manifestPath;
    }

    /// <summary>Hook for tests to simulate a failing disk.</summary>
    internal static Action<string, byte[]> AtomicWriter { get; set; } = AtomicWriteCore;

    internal static void AtomicWrite(string target, byte[] data) => AtomicWriter(target, data);

    private static void AtomicWriteCore(string target, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var tmp = Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(tmp, data);
            if (!OperatingSystem.IsWindows() && File.Exists(target))
            {
                File.SetUnixFileMode(tmp, File.GetUnixFileMode(target));
            }

            File.Move(tmp, target, overwrite: true);
        }
        catch
        {
            try { File.Delete(tmp); } catch (IOException) { }
            throw;
        }
    }

    internal static string? Sha(byte[]? data) => data is null ? null : Convert.ToHexStringLower(SHA256.HashData(data));

    /// <summary>
    /// Restore every file a commit touched. All-or-nothing: refuses if any of them was
    /// edited after the commit. Files a failed commit never reached are still in their
    /// original state and are left as they are.
    /// </summary>
    public static IReadOnlyList<string> Undo(string manifestPath)
    {
        var manifest = JsonSerializer.Deserialize(File.ReadAllText(manifestPath), JournalJson.Default.JournalManifest)
            ?? throw new InvalidDataException("empty journal manifest");
        if (manifest.Undone == true)
        {
            throw new ConflictException($"commit {manifest.Id} was already undone");
        }

        var root = manifest.Root;
        var edited = new List<string>();
        foreach (var e in manifest.Entries)
        {
            var target = Path.Combine(root, e.Path.Replace('/', Path.DirectorySeparatorChar));
            var current = Sha(File.Exists(target) ? File.ReadAllBytes(target) : null);
            if (current != e.AfterSha256 && current != e.BeforeSha256)
            {
                edited.Add(e.Path);
            }
        }

        if (edited.Count > 0)
        {
            throw new ConflictException($"edited since commit, refusing to undo: {string.Join(", ", edited)}");
        }

        var dir = Path.GetDirectoryName(manifestPath)!;
        for (var i = manifest.Entries.Count - 1; i >= 0; i--)
        {
            var e = manifest.Entries[i];
            var target = Path.Combine(root, e.Path.Replace('/', Path.DirectorySeparatorChar));
            if (e.Backup is null)
            {
                File.Delete(target);
            }
            else
            {
                AtomicWrite(target, File.ReadAllBytes(Path.Combine(dir, e.Backup)));
            }
        }

        foreach (var d in manifest.CreatedDirs.OrderByDescending(d => d.Length))
        {
            var path = Path.Combine(root, d.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }

        manifest.Undone = true;
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JournalJson.Default.JournalManifest));
        return [.. manifest.Entries.Select(e => e.Path)];
    }

    /// <summary>
    /// Resolve a path to its real location, following every symbolic link (and Windows
    /// reparse point) even when the final components do not exist yet.
    /// </summary>
    internal static string RealPath(string path)
    {
        var root = Path.GetPathRoot(path) ?? Path.DirectorySeparatorChar.ToString();
        var pending = new LinkedList<string>(SplitParts(path[root.Length..]));
        var current = root;
        var hops = 0;
        while (pending.Count > 0)
        {
            var part = pending.First!.Value;
            pending.RemoveFirst();
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar));
                current = string.IsNullOrEmpty(parent) ? root : parent;
                continue;
            }

            var next = Path.Combine(current, part);
            string? target = null;
            try
            {
                FileSystemInfo info = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
                target = info.LinkTarget;
            }
            catch (IOException)
            {
                // Not a link we can read; treat as an ordinary component.
            }

            if (target is null)
            {
                current = next;
                continue;
            }

            if (++hops > 40)
            {
                throw new IOException($"too many levels of symbolic links in {path}");
            }

            var resolved = Path.IsPathRooted(target) ? target : Path.Combine(current, target);
            var resolvedRoot = Path.GetPathRoot(resolved) ?? root;
            var rest = SplitParts(resolved[resolvedRoot.Length..]);
            for (var i = rest.Count - 1; i >= 0; i--)
            {
                pending.AddFirst(rest[i]);
            }

            current = resolvedRoot;
        }

        return current == root ? current : current.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static List<string> SplitParts(string s) =>
        [.. s.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)];
}
