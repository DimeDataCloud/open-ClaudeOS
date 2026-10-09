namespace ClaudeOS.Core.Layout;

public sealed record RestoreResult(IReadOnlyList<WindowMove> Moves, IReadOnlyList<long> Skipped);

/// <summary>
/// Remembers every window the engine moved so "put it back" restores the old layout exactly.
/// Moving other windows changes nothing in your files, so it needs no approval card, but every
/// change is still recorded and reversible.
/// </summary>
public sealed class LayoutHistory
{
    private readonly Stack<IReadOnlyList<WindowMove>> _stack = new();

    public int Depth => _stack.Count;

    public void Record(Placement placement)
    {
        if (placement.Moves.Count > 0)
        {
            _stack.Push(placement.Moves);
        }
    }

    /// <summary>The moves that undo the most recent layout change. A window whose bounds are no
    /// longer where the engine left it (you moved it yourself) is skipped, not fought over.</summary>
    public RestoreResult? PutBack(IReadOnlyList<WindowInfo> currentWindows)
    {
        if (_stack.Count == 0)
        {
            return null;
        }

        var recorded = _stack.Pop();
        var moves = new List<WindowMove>();
        var skipped = new List<long>();
        foreach (var move in recorded)
        {
            var current = currentWindows.FirstOrDefault(w => w.Handle == move.Handle);
            if (current is null || current.Bounds != move.To)
            {
                skipped.Add(move.Handle);
                continue;
            }

            moves.Add(new WindowMove(move.Handle, move.To, move.From));
        }

        return new RestoreResult(moves, skipped);
    }
}

public enum Region { LeftHalf, RightHalf, TopLeft, TopRight, BottomLeft, BottomRight, Center }

public sealed record Suggestion(string ContentKind, Region Region, string Message);

/// <summary>
/// Learns by suggestion only. If you keep dragging PDFs to the right half, it offers a rule
/// ("open PDFs on the right half?"). It never changes behaviour silently. What it has seen, and
/// which offers it has already made, can be saved and restored, so a restart does not forget a
/// habit and an answered offer is never repeated.
/// </summary>
public sealed class HabitTracker(int threshold = 3)
{
    private readonly Dictionary<(string Kind, Region Region), int> _counts = [];
    private readonly HashSet<(string, Region)> _offered = [];

    private const int MaxEntries = 64;
    private const int MaxCount = 1000;
    private const int MaxKind = 40;

    /// <summary>The tracker's memory as a small JSON document.</summary>
    public string Save()
    {
        using var stream = new MemoryStream();
        using (var w = new System.Text.Json.Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteNumber("v", 1);
            w.WriteStartArray("counts");
            foreach (var ((kind, region), n) in _counts.OrderBy(c => c.Key.Kind, StringComparer.Ordinal).ThenBy(c => c.Key.Region).Take(MaxEntries))
            {
                w.WriteStartObject();
                w.WriteString("kind", kind);
                w.WriteString("region", region.ToString());
                w.WriteNumber("n", Math.Min(n, MaxCount));
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteStartArray("offered");
            foreach (var (kind, region) in _offered.OrderBy(o => o.Item1, StringComparer.Ordinal).ThenBy(o => o.Item2).Take(MaxEntries))
            {
                w.WriteStartObject();
                w.WriteString("kind", kind);
                w.WriteString("region", region.ToString());
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Restore a saved tracker. The file is just a file on disk, so anything wrong with it (empty,
    /// truncated, hand-edited, hostile) gives a fresh tracker rather than an error: forgetting a
    /// habit is harmless, refusing to start is not.
    /// </summary>
    public static HabitTracker Load(string? json, int threshold = 3)
    {
        var tracker = new HabitTracker(threshold);
        if (string.IsNullOrWhiteSpace(json) || json.Length > 64 * 1024)
        {
            return tracker;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return tracker;
            }

            if (root.TryGetProperty("counts", out var counts) && counts.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var e in counts.EnumerateArray().Take(MaxEntries))
                {
                    if (Entry(e) is { } key && e.TryGetProperty("n", out var n) && n.TryGetInt32(out var count) && count > 0)
                    {
                        tracker._counts[key] = Math.Min(count, MaxCount);
                    }
                }
            }

            if (root.TryGetProperty("offered", out var offered) && offered.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var e in offered.EnumerateArray().Take(MaxEntries))
                {
                    if (Entry(e) is { } key)
                    {
                        tracker._offered.Add(key);
                    }
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return new HabitTracker(threshold);
        }

        return tracker;
    }

    private static (string, Region)? Entry(System.Text.Json.JsonElement e)
    {
        if (e.ValueKind != System.Text.Json.JsonValueKind.Object
            || !e.TryGetProperty("kind", out var kind) || kind.ValueKind != System.Text.Json.JsonValueKind.String
            || !e.TryGetProperty("region", out var region) || region.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return null;
        }

        var name = kind.GetString()!;
        return name.Length is > 0 and <= MaxKind && Enum.TryParse<Region>(region.GetString(), out var r) && Enum.IsDefined(r) ? (name, r) : null;
    }

    public static Region RegionOf(Rect rect, Rect workArea)
    {
        var (cx, cy) = rect.Center;
        var relX = (cx - workArea.X) / workArea.Width;
        var relY = (cy - workArea.Y) / workArea.Height;
        var wide = rect.Height >= workArea.Height * 0.8;
        if (wide)
        {
            return relX < 0.5 ? Region.LeftHalf : Region.RightHalf;
        }

        var centered = relX is > 0.35 and < 0.65 && relY is > 0.35 and < 0.65;
        if (centered)
        {
            return Region.Center;
        }

        return (relX < 0.5, relY < 0.5) switch
        {
            (true, true) => Region.TopLeft,
            (false, true) => Region.TopRight,
            (true, false) => Region.BottomLeft,
            _ => Region.BottomRight,
        };
    }

    /// <summary>Record where you put a piece of content after it opened. Returns a suggestion
    /// the first time the same choice has been made <c>threshold</c> times.</summary>
    public Suggestion? Record(string contentKind, Rect finalBounds, Rect workArea)
    {
        var key = (contentKind, RegionOf(finalBounds, workArea));
        _counts[key] = _counts.GetValueOrDefault(key) + 1;
        if (_counts[key] >= threshold && _offered.Add(key))
        {
            var where = key.Item2 switch
            {
                Region.LeftHalf => "the left half",
                Region.RightHalf => "the right half",
                Region.TopLeft => "the top left",
                Region.TopRight => "the top right",
                Region.BottomLeft => "the bottom left",
                Region.BottomRight => "the bottom right",
                _ => "the centre",
            };
            return new Suggestion(contentKind, key.Item2, $"Open {contentKind} on {where} from now on?");
        }

        return null;
    }
}
