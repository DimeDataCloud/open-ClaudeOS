using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeOS.Core.Safety;

/// <summary>Where the orchestrator keeps its own records: undo journal, outbox, audit log.</summary>
public sealed class StateDir
{
    private readonly TimeProvider _clock;
    private readonly object _gate = new();

    public StateDir(string? path = null, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        Path = path ?? DefaultPath();
        Journal = System.IO.Path.Combine(Path, "journal");
        Outbox = System.IO.Path.Combine(Path, "outbox");
        AuditLog = System.IO.Path.Combine(Path, "audit.jsonl");
        Directory.CreateDirectory(Journal);
        Directory.CreateDirectory(Outbox);
    }

    public string Path { get; }

    public string Journal { get; }

    public string Outbox { get; }

    public string AuditLog { get; }

    public static string DefaultPath()
    {
        var env = Environment.GetEnvironmentVariable("CLAUDEOS_STATE_DIR");
        if (!string.IsNullOrEmpty(env))
        {
            return env;
        }

        if (OperatingSystem.IsWindows())
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeOS");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return System.IO.Path.Combine(string.IsNullOrEmpty(xdg) ? System.IO.Path.Combine(home, ".local", "state") : xdg, "claudeos");
    }

    /// <summary>Append-only audit trail of what was read, proposed, approved and done.</summary>
    public void Log(string @event, JsonObject? data = null)
    {
        var record = new JsonObject
        {
            ["ts"] = _clock.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ["event"] = @event,
        };
        if (data is not null)
        {
            foreach (var (key, value) in data.ToList())
            {
                data.Remove(key);
                record[key] = value;
            }
        }

        lock (_gate)
        {
            File.AppendAllText(AuditLog, record.ToJsonString() + "\n");
        }
    }

    public IReadOnlyList<JsonObject> ReadLog(int limit = 20)
    {
        if (!File.Exists(AuditLog))
        {
            return [];
        }

        return [.. File.ReadAllLines(AuditLog).TakeLast(limit).Select(l => JsonNode.Parse(l)!.AsObject())];
    }

    /// <summary>Undo manifests, oldest first (commit ids start with a timestamp).</summary>
    public IReadOnlyList<string> Commits() =>
        [.. Directory.EnumerateDirectories(Journal).Order(StringComparer.Ordinal)
            .Select(d => System.IO.Path.Combine(d, "manifest.json")).Where(File.Exists)];
}

/// <summary>Tiny helpers for building audit records without reflection-based serialization.</summary>
public static class Audit
{
    public static JsonObject Of(params (string Key, JsonNode? Value)[] fields)
    {
        var o = new JsonObject();
        foreach (var (key, value) in fields)
        {
            o[key] = value;
        }

        return o;
    }

    public static JsonNode? Str(string? s) => s is null ? null : JsonValue.Create(s);

    public static JsonArray Strs(IEnumerable<string> items) => new([.. items.Select(i => (JsonNode?)JsonValue.Create(i))]);

    public static JsonNode? Parse(string json) => JsonNode.Parse(json);
}
