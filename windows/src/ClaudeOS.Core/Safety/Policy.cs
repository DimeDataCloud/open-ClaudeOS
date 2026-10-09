using System.Collections.Immutable;
using System.Text;
using ClaudeOS.Core.Actions;

namespace ClaudeOS.Core.Safety;

public sealed class PolicyDeniedException(string message) : Exception(message);

public enum Risk
{
    /// <summary>Creates new files; nothing existing is touched.</summary>
    Low = 1,

    /// <summary>Changes or removes existing files; undoable after commit.</summary>
    Medium = 2,

    /// <summary>Leaves the machine; cannot be undone.</summary>
    High = 3,
}

public sealed record Assessment(PlanAction Action, Risk Risk, ImmutableArray<string> Notes = default, string? Denied = null)
{
    public ImmutableArray<string> Notes { get; init; } = Notes.IsDefault ? [] : Notes;
}

/// <summary>
/// Deterministic policy: what may be touched, and how risky each action is. No model is
/// consulted here. The model's job is to propose; this decides what is refused outright and
/// what the person must be told before consenting.
/// </summary>
public sealed record Policy
{
    /// <summary>Matched against every component of a workspace-relative path, ignoring case
    /// (Windows and macOS file systems do). Secrets are denied for reading too: what the
    /// agent cannot read, it cannot leak.</summary>
    public static readonly ImmutableArray<string> DefaultProtected =
    [
        ".ssh", ".gnupg", ".aws", ".azure", ".kube", ".docker", ".git", ".netrc", ".npmrc", ".pypirc",
        ".env", ".env.*", "*.pem", "*.key", "*.p12", "*.pfx", "*.ppk", "*.kdbx",
        "id_rsa*", "id_ed25519*", "id_ecdsa*", "NTUSER.DAT*",
    ];

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public ImmutableArray<string> Protected { get; init; } = DefaultProtected;

    public ImmutableHashSet<string> TrustedEmailDomains { get; init; } = [];

    public ImmutableHashSet<string> TrustedHosts { get; init; } = [];

    public long MaxWriteBytes { get; init; } = 5_000_000;

    public string CheckPath(Overlay overlay, string path)
    {
        string rel;
        try
        {
            rel = overlay.Rel(path);
        }
        catch (PathEscapeException e)
        {
            throw new PolicyDeniedException(e.Message);
        }

        if (rel == ".")
        {
            return rel;
        }

        foreach (var part in rel.Split('/'))
        {
            if (Protected.Any(pattern => Glob.Match(part, pattern)))
            {
                throw new PolicyDeniedException($"{rel} is a protected path");
            }

            // Names that mean something else on Windows: alternate data streams, device names,
            // and trailing dots or spaces that Windows silently strips. Refused everywhere so a
            // plan means the same thing on every platform.
            if (part.Contains(':') || part.EndsWith('.') || part.EndsWith(' ') || ReservedDeviceNames.Contains(part.Split('.')[0]) || part.Length > 255)
            {
                throw new PolicyDeniedException($"{rel} is not a portable file name");
            }
        }

        return rel;
    }

    public bool IsProtectedName(string name) => Protected.Any(pattern => Glob.Match(name.TrimEnd('/'), pattern));

    public Assessment Assess(PlanAction action, Overlay overlay)
    {
        try
        {
            var (risk, notes) = AssessCore(action, overlay);
            return new Assessment(action, risk, [.. notes]);
        }
        catch (Exception e) when (e is PolicyDeniedException or IOException or UnauthorizedAccessException)
        {
            return new Assessment(action, action.Effect == Effect.External ? Risk.High : Risk.Medium, Denied: e.Message);
        }
    }

    private (Risk, List<string>) AssessCore(PlanAction action, Overlay overlay)
    {
        switch (action)
        {
            case WriteFile w:
                {
                    var rel = CheckPath(overlay, w.Path);
                    var size = Encoding.UTF8.GetByteCount(w.Content);
                    if (size > MaxWriteBytes)
                    {
                        throw new PolicyDeniedException($"{rel}: {size} bytes exceeds the {MaxWriteBytes}-byte write limit");
                    }

                    if (overlay.IsDir(rel))
                    {
                        throw new PolicyDeniedException($"{rel} is a directory");
                    }

                    return overlay.Read(rel) is null ? (Risk.Low, ["new file"]) : (Risk.Medium, ["overwrites existing file"]);
                }

            case DeleteFile d:
                {
                    var rel = CheckPath(overlay, d.Path);
                    if (overlay.Read(rel) is null)
                    {
                        throw new PolicyDeniedException($"{rel} does not exist");
                    }

                    return (Risk.Medium, ["deletes file (restorable with undo)"]);
                }

            case MoveFile m:
                {
                    var src = CheckPath(overlay, m.Src);
                    var dst = CheckPath(overlay, m.Dst);
                    if (overlay.Read(src) is null)
                    {
                        throw new PolicyDeniedException($"{src} does not exist");
                    }

                    if (overlay.IsDir(dst))
                    {
                        throw new PolicyDeniedException($"{dst} is a directory; give the full destination file path");
                    }

                    return overlay.Read(dst) is not null
                        ? (Risk.Medium, ["overwrites existing file at destination"])
                        : (Risk.Medium, ["moves existing file"]);
                }

            case SendEmail e:
                {
                    var notes = new List<string> { "leaves this machine; cannot be undone" };
                    notes.AddRange(e.Domains().Where(d => !TrustedEmailDomains.Contains(d)).Order(StringComparer.Ordinal)
                        .Select(d => $"recipient domain not on your trusted list: {d}"));
                    return (Risk.High, notes);
                }

            case HttpRequest h:
                {
                    var notes = new List<string> { "leaves this machine; cannot be undone" };
                    if (h.Method is not ("GET" or "HEAD"))
                    {
                        notes.Add($"{h.Method} sends data to the remote host");
                    }

                    if (!TrustedHosts.Contains(h.Host()))
                    {
                        notes.Add($"host not on your trusted list: {h.Host()}");
                    }

                    return (Risk.High, notes);
                }

            default:
                throw new PolicyDeniedException($"no policy rule for action type '{action.Kind}'");
        }
    }

    /// <summary>
    /// Assess each action against the state left by the ones before it, and stage the local
    /// ones that pass, so <see cref="Overlay.Diff"/> shows the result.
    /// </summary>
    public IReadOnlyList<Assessment> Preview(Plan plan, Overlay overlay)
    {
        var result = new List<Assessment>();
        foreach (var action in plan.Actions)
        {
            var a = Assess(action, overlay);
            if (a.Denied is null && action.Effect == Effect.Local)
            {
                try
                {
                    Stage(action, overlay);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    a = a with { Denied = e.Message };
                }
            }

            result.Add(a);
        }

        return result;
    }

    public static void Stage(PlanAction action, Overlay overlay)
    {
        switch (action)
        {
            case WriteFile w:
                overlay.Write(w.Path, Encoding.UTF8.GetBytes(w.Content));
                break;
            case DeleteFile d:
                overlay.Delete(d.Path);
                break;
            case MoveFile m:
                overlay.Move(m.Src, m.Dst);
                break;
            default:
                throw new InvalidOperationException($"{action.Kind} is not a local action");
        }
    }
}

/// <summary>A small <c>fnmatch</c>: <c>*</c>, <c>?</c> and <c>[set]</c>, case-insensitive.</summary>
internal static class Glob
{
    public static bool Match(string name, string pattern) => Match(name.AsSpan(), pattern.AsSpan());

    private static bool Match(ReadOnlySpan<char> s, ReadOnlySpan<char> p)
    {
        while (true)
        {
            if (p.IsEmpty)
            {
                return s.IsEmpty;
            }

            switch (p[0])
            {
                case '*':
                    for (var i = 0; i <= s.Length; i++)
                    {
                        if (Match(s[i..], p[1..]))
                        {
                            return true;
                        }
                    }

                    return false;
                case '?':
                    if (s.IsEmpty)
                    {
                        return false;
                    }

                    break;
                case '[':
                    {
                        var close = p.IndexOf(']');
                        if (close > 1 && !s.IsEmpty)
                        {
                            var set = p[1..close];
                            var negate = set.Length > 0 && set[0] == '!';
                            if (negate)
                            {
                                set = set[1..];
                            }

                            var hit = false;
                            for (var i = 0; i < set.Length; i++)
                            {
                                if (i + 2 < set.Length && set[i + 1] == '-')
                                {
                                    hit |= char.ToLowerInvariant(s[0]) >= char.ToLowerInvariant(set[i]) && char.ToLowerInvariant(s[0]) <= char.ToLowerInvariant(set[i + 2]);
                                    i += 2;
                                }
                                else
                                {
                                    hit |= char.ToLowerInvariant(s[0]) == char.ToLowerInvariant(set[i]);
                                }
                            }

                            if (hit == negate)
                            {
                                return false;
                            }

                            s = s[1..];
                            p = p[(close + 1)..];
                            continue;
                        }

                        if (s.IsEmpty || char.ToLowerInvariant(s[0]) != '[')
                        {
                            return false;
                        }

                        break;
                    }

                default:
                    if (s.IsEmpty || char.ToLowerInvariant(s[0]) != char.ToLowerInvariant(p[0]))
                    {
                        return false;
                    }

                    break;
            }

            s = s[1..];
            p = p[1..];
        }
    }
}
