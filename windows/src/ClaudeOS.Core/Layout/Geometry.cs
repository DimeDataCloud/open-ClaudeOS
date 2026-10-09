namespace ClaudeOS.Core.Layout;

/// <summary>A size in physical pixels.</summary>
public readonly record struct Size(int Width, int Height)
{
    public long Area => (long)Width * Height;

    public double AspectRatio => Height == 0 ? 0 : (double)Width / Height;
}

/// <summary>A rectangle in physical (screen) pixels. Every coordinate in the layout engine is
/// physical; <see cref="Scaling"/> is the one place that converts to and from device-independent
/// units, so 150% and 200% displays cannot drift apart.</summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public long Area => Width <= 0 || Height <= 0 ? 0 : (long)Width * Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public (double X, double Y) Center => (X + Width / 2.0, Y + Height / 2.0);

    public bool Intersects(Rect other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    public bool Contains(Rect other) => other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;

    public Rect Intersect(Rect other)
    {
        var x = Math.Max(X, other.X);
        var y = Math.Max(Y, other.Y);
        var r = Math.Min(Right, other.Right);
        var b = Math.Min(Bottom, other.Bottom);
        return r <= x || b <= y ? default : new Rect(x, y, r - x, b - y);
    }

    public Rect Inflate(int by) => new(X - by, Y - by, Width + 2 * by, Height + 2 * by);

    public Rect Deflate(int by) => new(X + by, Y + by, Math.Max(0, Width - 2 * by), Math.Max(0, Height - 2 * by));

    public static Rect FromLtrb(int left, int top, int right, int bottom) => new(left, top, right - left, bottom - top);

    public override string ToString() => $"{Width}x{Height}@{X},{Y}";
}

/// <summary>Conversion between device-independent pixels (what XAML uses) and physical pixels.</summary>
public static class Scaling
{
    public static int ToPhysical(double dips, double scale) => (int)Math.Round(dips * scale);

    public static double ToDips(int physical, double scale) => physical / scale;

    public static Size ToPhysical(double widthDips, double heightDips, double scale) =>
        new(ToPhysical(widthDips, scale), ToPhysical(heightDips, scale));
}
