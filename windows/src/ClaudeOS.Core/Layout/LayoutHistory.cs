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
/// ("open PDFs on the right half?"). It never changes behaviour silently.
/// </summary>
public sealed class HabitTracker(int threshold = 3)
{
    private readonly Dictionary<(string Kind, Region Region), int> _counts = [];
    private readonly HashSet<(string, Region)> _offered = [];

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
