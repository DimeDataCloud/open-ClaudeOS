namespace ClaudeOS.Core.Layout;

public enum Anchor { Auto, TopLeft, TopRight, BottomLeft, BottomRight, Center, LeftHalf, RightHalf }

/// <summary>How a placement was reached, in the order the engine tries them.</summary>
public enum PlacementMethod
{
    /// <summary>Free space on screen: nothing was covered or moved.</summary>
    FreeSpace,

    /// <summary>Free space, but smaller than asked for.</summary>
    FreeSpaceShrunk,

    /// <summary>Floats over a background window; the active window stays visible.</summary>
    OverBackground,

    /// <summary>The active window was snapped to half the screen to make room.</summary>
    MadeRoom,

    /// <summary>Centred in focus mode; dismissing it returns everything as it was.</summary>
    FocusMode,
}

public sealed record WindowMove(long Handle, Rect From, Rect To);

public sealed record PlacementRequest(
    Size Desired,
    Size Minimum = default,
    Anchor Anchor = Anchor.Auto,
    long? IgnoreWindow = null)
{
    public Size Minimum { get; init; } = Minimum == default ? new Size(Desired.Width / 2, Desired.Height / 2) : Minimum;
}

public sealed record Placement(Rect Bounds, PlacementMethod Method, string MonitorId, IReadOnlyList<WindowMove> Moves, string Reason)
{
    /// <summary>True when other windows had to move so the content could be shown.</summary>
    public bool MovesOtherWindows => Moves.Count > 0;
}

public sealed record LayoutOptions
{
    /// <summary>Gap between windows and the screen edge, in device-independent pixels (the same
    /// small gap Windows' Snap layouts leave).</summary>
    public int GapDips { get; init; } = 8;

    /// <summary>Smallest scale the content may be shrunk to before a fallback is tried.</summary>
    public double MinShrink { get; init; } = 0.7;

    /// <summary>Fallbacks, in order. Each is configurable and can be turned off.</summary>
    public IReadOnlyList<PlacementMethod> Fallbacks { get; init; } =
        [PlacementMethod.OverBackground, PlacementMethod.MadeRoom, PlacementMethod.FocusMode];
}

/// <summary>
/// "Understand where everything else is": plain geometry, no model, milliseconds. Looks at the
/// monitor you are working on, finds the largest empty rectangles left by the visible windows,
/// scores each place the content fits, and falls back in a fixed, configurable order when
/// nothing fits.
/// </summary>
public static class LayoutEngine
{
    public static Placement Place(Desktop desktop, PlacementRequest request, LayoutOptions? options = null)
    {
        options ??= new LayoutOptions();
        var monitor = desktop.FocusMonitor;
        var gap = Scaling.ToPhysical(options.GapDips, monitor.Scale);
        var area = monitor.WorkArea.Deflate(gap);
        var active = desktop.ActiveWindow;

        var obstacles = desktop.Windows
            .Where(w => w.IsVisible && w.MonitorId == monitor.Id && w.Handle != request.IgnoreWindow)
            .ToList();

        // Tablet posture: apps fill the screen and there is no pointer to reach a corner, so
        // centre the content in focus mode unless a comfortable gap already exists.
        var free = FreeSpace.MaximalEmptyRects(area, obstacles.Select(w => w.Bounds.Inflate(gap / 2)));

        foreach (var scale in ShrinkSteps(options.MinShrink))
        {
            var size = Scale(request.Desired, scale, request.Minimum);
            if (scale < 1 && size == request.Desired)
            {
                continue;
            }

            if (PickInFree(free, size, request.Anchor, active, area) is { } rect)
            {
                var method = scale >= 1 ? PlacementMethod.FreeSpace : PlacementMethod.FreeSpaceShrunk;
                var reason = scale >= 1 ? "free space on screen" : $"free space, shrunk to {(int)Math.Round(scale * 100)}% to fit";
                return new Placement(rect, method, monitor.Id, [], reason);
            }
        }

        var tablet = desktop.Posture == Posture.Tablet;
        foreach (var method in options.Fallbacks)
        {
            switch (method)
            {
                case PlacementMethod.OverBackground when !tablet:
                    if (FloatOverBackground(area, request, obstacles, active, options.MinShrink) is { } over)
                    {
                        return new Placement(over, method, monitor.Id, [], "no free space; floating over a background window, the active window stays visible");
                    }

                    break;

                case PlacementMethod.MadeRoom when !tablet:
                    if (MakeRoom(monitor, area, gap, request, active) is { } room)
                    {
                        return new Placement(room.Bounds, method, monitor.Id, room.Moves, "no free space; snapped the active window to half the screen");
                    }

                    break;

                case PlacementMethod.FocusMode:
                    return new Placement(Centered(area, FitInto(request.Desired, area)), method, monitor.Id, [],
                        tablet ? "tablet posture; showing centred in focus mode" : "no room anywhere else; showing centred in focus mode");
            }
        }

        // Fallbacks may all be switched off. Showing centred is still better than failing.
        return new Placement(Centered(area, FitInto(request.Desired, area)), PlacementMethod.FocusMode, monitor.Id, [], "all fallbacks disabled; centred");
    }

    private static IEnumerable<double> ShrinkSteps(double minShrink)
    {
        yield return 1.0;
        for (var s = 0.9; s >= minShrink - 1e-9; s -= 0.1)
        {
            yield return Math.Round(s, 2);
        }
    }

    private static Size Scale(Size desired, double scale, Size minimum)
    {
        var w = (int)Math.Round(desired.Width * scale);
        var h = (int)Math.Round(desired.Height * scale);
        return new Size(Math.Max(w, Math.Min(minimum.Width, desired.Width)), Math.Max(h, Math.Min(minimum.Height, desired.Height)));
    }

    private static Rect? PickInFree(IReadOnlyList<Rect> free, Size size, Anchor anchor, WindowInfo? active, Rect area)
    {
        Rect? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var f in free.Where(f => f.Width >= size.Width && f.Height >= size.Height))
        {
            foreach (var (candidate, corner) in Candidates(f, size))
            {
                var score = Score(candidate, corner, anchor, active, area, f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
        }

        return best;
    }

    private static IEnumerable<(Rect Rect, Anchor Corner)> Candidates(Rect free, Size s)
    {
        yield return (new Rect(free.X, free.Y, s.Width, s.Height), Anchor.TopLeft);
        yield return (new Rect(free.Right - s.Width, free.Y, s.Width, s.Height), Anchor.TopRight);
        yield return (new Rect(free.X, free.Bottom - s.Height, s.Width, s.Height), Anchor.BottomLeft);
        yield return (new Rect(free.Right - s.Width, free.Bottom - s.Height, s.Width, s.Height), Anchor.BottomRight);
        yield return (new Rect(free.X + (free.Width - s.Width) / 2, free.Y + (free.Height - s.Height) / 2, s.Width, s.Height), Anchor.Center);
    }

    /// <summary>Higher is better. Anchor match and tidy edges dominate; nearness to the active
    /// window breaks ties, and big empty areas are left big when there is a choice.</summary>
    private static double Score(Rect candidate, Anchor corner, Anchor wanted, WindowInfo? active, Rect area, Rect free)
    {
        double score = 0;

        if (wanted != Anchor.Auto)
        {
            score += AnchorMatch(corner, wanted) * 100;
        }
        else
        {
            // No preference: tuck into a corner, right side first, then top (out of the way of
            // reading order, like notifications).
            score += corner switch
            {
                Anchor.TopRight => 12,
                Anchor.BottomRight => 10,
                Anchor.TopLeft => 8,
                Anchor.BottomLeft => 7,
                _ => 0,
            };
        }

        // Flush against the screen edges reads as deliberate.
        if (candidate.X == area.X || candidate.Right == area.Right)
        {
            score += 6;
        }

        if (candidate.Y == area.Y || candidate.Bottom == area.Bottom)
        {
            score += 6;
        }

        if (active is not null)
        {
            var (ax, ay) = active.Bounds.Center;
            var (cx, cy) = candidate.Center;
            var diag = Math.Sqrt((double)area.Width * area.Width + (double)area.Height * area.Height);
            var distance = Math.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy)) / diag;
            score += (1 - distance) * 20;
        }

        // Prefer a snug free area so the biggest ones stay available for the next window.
        score -= (double)free.Area / Math.Max(1, area.Area) * 4;
        return score;
    }

    private static double AnchorMatch(Anchor corner, Anchor wanted) => (corner, wanted) switch
    {
        _ when corner == wanted => 1,
        (Anchor.TopRight or Anchor.BottomRight, Anchor.RightHalf) => 0.8,
        (Anchor.TopLeft or Anchor.BottomLeft, Anchor.LeftHalf) => 0.8,
        (Anchor.TopRight, Anchor.BottomRight) or (Anchor.BottomRight, Anchor.TopRight) => 0.3,
        (Anchor.TopLeft, Anchor.BottomLeft) or (Anchor.BottomLeft, Anchor.TopLeft) => 0.3,
        (Anchor.TopLeft, Anchor.TopRight) or (Anchor.TopRight, Anchor.TopLeft) => 0.3,
        (Anchor.BottomLeft, Anchor.BottomRight) or (Anchor.BottomRight, Anchor.BottomLeft) => 0.3,
        _ => 0,
    };

    /// <summary>Floats at the preferred corner, over background windows only. The result must not
    /// touch the active window, so what you are working on is never covered.</summary>
    private static Rect? FloatOverBackground(Rect area, PlacementRequest request, IReadOnlyList<WindowInfo> windows, WindowInfo? active, double minShrink)
    {
        // Try the full size first, then smaller: a slightly smaller window that leaves the active
        // one alone beats making room by moving it.
        foreach (var scale in ShrinkSteps(minShrink))
        {
            var size = FitInto(Scale(request.Desired, scale, request.Minimum), area);
            if (FloatOverBackground(area, request, size, windows, active) is { } rect)
            {
                return rect;
            }
        }

        return null;
    }

    private static Rect? FloatOverBackground(Rect area, PlacementRequest request, Size size, IReadOnlyList<WindowInfo> windows, WindowInfo? active)
    {
        var corners = new[]
        {
            (new Rect(area.Right - size.Width, area.Y, size.Width, size.Height), Anchor.TopRight),
            (new Rect(area.Right - size.Width, area.Bottom - size.Height, size.Width, size.Height), Anchor.BottomRight),
            (new Rect(area.X, area.Y, size.Width, size.Height), Anchor.TopLeft),
            (new Rect(area.X, area.Bottom - size.Height, size.Width, size.Height), Anchor.BottomLeft),
        };

        Rect? best = null;
        var bestScore = double.NegativeInfinity;
        foreach (var (rect, corner) in corners)
        {
            if (active is not null && rect.Intersects(active.Bounds))
            {
                continue;
            }

            // Fewer covered pixels is better, then the anchor preference.
            double covered = windows.Where(w => w.Handle != active?.Handle).Sum(w => (double)rect.Intersect(w.Bounds).Area);
            var score = -covered / Math.Max(1, rect.Area) * 10 + (request.Anchor == Anchor.Auto ? 0 : AnchorMatch(corner, request.Anchor) * 5);
            if (score > bestScore)
            {
                bestScore = score;
                best = rect;
            }
        }

        return best;
    }

    private sealed record Room(Rect Bounds, IReadOnlyList<WindowMove> Moves);

    /// <summary>Snaps the active window to the half of the screen it mostly occupies and uses
    /// the other half. Splits top and bottom on a portrait screen.</summary>
    private static Room? MakeRoom(MonitorInfo monitor, Rect area, int gap, PlacementRequest request, WindowInfo? active)
    {
        if (active is null || !active.CanMove)
        {
            return null;
        }

        Rect keep, other;
        if (monitor.IsPortrait)
        {
            var half = (area.Height - gap) / 2;
            var topBias = active.Bounds.Center.Y <= area.Y + area.Height / 2.0;
            var top = new Rect(area.X, area.Y, area.Width, half);
            var bottom = new Rect(area.X, area.Y + half + gap, area.Width, area.Height - half - gap);
            (keep, other) = topBias ? (top, bottom) : (bottom, top);
        }
        else
        {
            var half = (area.Width - gap) / 2;
            var leftBias = active.Bounds.Center.X <= area.X + area.Width / 2.0;
            var left = new Rect(area.X, area.Y, half, area.Height);
            var right = new Rect(area.X + half + gap, area.Y, area.Width - half - gap, area.Height);
            (keep, other) = leftBias ? (left, right) : (right, left);
        }

        var size = FitInto(request.Desired, other);
        var placed = Centered(other, size);
        return new Room(placed, [new WindowMove(active.Handle, active.Bounds, keep)]);
    }

    private static Size FitInto(Size desired, Rect area)
    {
        if (desired.Width <= area.Width && desired.Height <= area.Height)
        {
            return desired;
        }

        var k = Math.Min((double)area.Width / desired.Width, (double)area.Height / desired.Height);
        return new Size((int)(desired.Width * k), (int)(desired.Height * k));
    }

    private static Rect Centered(Rect area, Size size) =>
        new(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2, size.Width, size.Height);
}
