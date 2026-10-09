namespace ClaudeOS.Core.Layout;

/// <summary>Finds the maximal empty rectangles left in an area once obstacles are removed.</summary>
public static class FreeSpace
{
    /// <summary>
    /// Every empty rectangle in <paramref name="area"/> that cannot be made bigger without
    /// hitting an obstacle or the edge. Works on the grid the obstacle edges define, so the cost
    /// depends on how many windows there are, not on how many pixels the screen has.
    /// </summary>
    public static IReadOnlyList<Rect> MaximalEmptyRects(Rect area, IEnumerable<Rect> obstacles)
    {
        if (area.IsEmpty)
        {
            return [];
        }

        var blocked = obstacles.Select(o => o.Intersect(area)).Where(o => !o.IsEmpty).ToList();
        var xs = new SortedSet<int> { area.X, area.Right };
        var ys = new SortedSet<int> { area.Y, area.Bottom };
        foreach (var o in blocked)
        {
            xs.Add(o.X);
            xs.Add(o.Right);
            ys.Add(o.Y);
            ys.Add(o.Bottom);
        }

        var x = xs.ToArray();
        var y = ys.ToArray();
        int nx = x.Length - 1, ny = y.Length - 1;
        var free = new bool[ny, nx];
        for (var j = 0; j < ny; j++)
        {
            for (var i = 0; i < nx; i++)
            {
                var cell = new Rect(x[i], y[j], x[i + 1] - x[i], y[j + 1] - y[j]);
                free[j, i] = !blocked.Any(o => o.Intersects(cell));
            }
        }

        var result = new List<Rect>();
        var columnFree = new bool[nx];
        for (var top = 0; top < ny; top++)
        {
            Array.Fill(columnFree, true);
            for (var bottom = top; bottom < ny; bottom++)
            {
                for (var i = 0; i < nx; i++)
                {
                    columnFree[i] &= free[bottom, i];
                }

                for (var i = 0; i < nx;)
                {
                    if (!columnFree[i])
                    {
                        i++;
                        continue;
                    }

                    var start = i;
                    while (i < nx && columnFree[i])
                    {
                        i++;
                    }

                    var end = i - 1;
                    var canGrowUp = top > 0 && RowFree(free, top - 1, start, end);
                    var canGrowDown = bottom < ny - 1 && RowFree(free, bottom + 1, start, end);
                    if (!canGrowUp && !canGrowDown)
                    {
                        result.Add(new Rect(x[start], y[top], x[end + 1] - x[start], y[bottom + 1] - y[top]));
                    }
                }
            }
        }

        return result;
    }

    private static bool RowFree(bool[,] free, int row, int from, int to)
    {
        for (var i = from; i <= to; i++)
        {
            if (!free[row, i])
            {
                return false;
            }
        }

        return true;
    }
}
