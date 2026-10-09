using System.Diagnostics;
using ClaudeOS.Core.Layout;

namespace ClaudeOS.Core.Tests.Layout;

/// <summary>The arrangements from the Surface test checklist, as geometry: an empty desktop, one
/// maximized app, two apps snapped side by side, an external monitor, and the keyboard detached
/// in portrait. All numbers are physical pixels on a 2880x1920 panel at 200%.</summary>
public sealed class LayoutEngineTests
{
    // 2880x1920 with an 80px taskbar: the work area is 2880x1840.
    private static readonly MonitorInfo Surface = new("surface", new Rect(0, 0, 2880, 1840), 2.0, true);
    private static readonly Size Chart = new(840, 560);

    private static WindowInfo Win(long h, Rect bounds, bool active = false, bool maximized = false, string monitor = "surface", bool canMove = true) =>
        new(h, $"w{h}", "app.exe", bounds, monitor, active, IsMaximized: maximized, CanMove: canMove);

    private static Desktop Of(Posture posture, MonitorInfo[] monitors, string focus, params WindowInfo[] windows) =>
        new(monitors, windows, focus, posture);

    private static Desktop Laptop(params WindowInfo[] windows) => Of(Posture.Laptop, [Surface], "surface", windows);

    [Fact]
    public void An_empty_desktop_places_the_content_in_a_tidy_corner_without_moving_anything()
    {
        var p = LayoutEngine.Place(Laptop(), new PlacementRequest(Chart));

        Assert.Equal(PlacementMethod.FreeSpace, p.Method);
        Assert.Equal(new Size(840, 560), new Size(p.Bounds.Width, p.Bounds.Height));
        Assert.Empty(p.Moves);
        Assert.True(Surface.WorkArea.Contains(p.Bounds));
        Assert.Equal(2880 - 16, p.Bounds.Right); // top right, one gap (8 dips at 200%) from the edge
        Assert.Equal(16, p.Bounds.Y);
    }

    [Fact]
    public void Content_lands_in_the_free_space_beside_a_working_window_and_never_covers_it()
    {
        var editor = Win(1, new Rect(0, 0, 1700, 1840), active: true);
        var p = LayoutEngine.Place(Laptop(editor), new PlacementRequest(Chart));

        Assert.Equal(PlacementMethod.FreeSpace, p.Method);
        Assert.False(p.Bounds.Intersects(editor.Bounds));
        Assert.True(p.Bounds.X >= editor.Bounds.Right);
    }

    [Fact]
    public void One_maximized_app_is_snapped_to_half_and_the_content_takes_the_other_half()
    {
        var app = Win(1, Surface.WorkArea, active: true, maximized: true);
        var p = LayoutEngine.Place(Laptop(app), new PlacementRequest(Chart));

        Assert.Equal(PlacementMethod.MadeRoom, p.Method);
        var move = Assert.Single(p.Moves);
        Assert.Equal(1, move.Handle);
        Assert.Equal(app.Bounds, move.From);
        Assert.False(p.Bounds.Intersects(move.To));
        Assert.True(move.To.Width < app.Bounds.Width);
        Assert.True(Surface.WorkArea.Contains(p.Bounds));
    }

    [Fact]
    public void Two_snapped_apps_get_a_float_over_the_background_one_and_the_active_one_stays_clear()
    {
        var left = Win(1, new Rect(0, 0, 1436, 1840), active: true);
        var right = Win(2, new Rect(1444, 0, 1436, 1840));
        var p = LayoutEngine.Place(Laptop(left, right), new PlacementRequest(Chart));

        Assert.Equal(PlacementMethod.OverBackground, p.Method);
        Assert.False(p.Bounds.Intersects(left.Bounds));
        Assert.True(p.Bounds.Intersects(right.Bounds));
        Assert.Empty(p.Moves);
    }

    [Fact]
    public void With_an_external_monitor_the_content_goes_to_the_monitor_you_are_working_on()
    {
        var external = new MonitorInfo("dell", new Rect(2880, 0, 2560, 1400), 1.0);
        var onSurface = Win(1, new Rect(0, 0, 2880, 1840), monitor: "surface", maximized: true);
        var onDell = Win(2, new Rect(2880, 0, 1200, 1400), active: true, monitor: "dell");
        var p = LayoutEngine.Place(Of(Posture.Laptop, [Surface, external], "dell", onSurface, onDell), new PlacementRequest(Chart));

        Assert.Equal("dell", p.MonitorId);
        Assert.Equal(PlacementMethod.FreeSpace, p.Method);
        Assert.True(external.WorkArea.Contains(p.Bounds));
        Assert.False(p.Bounds.Intersects(onDell.Bounds));
        Assert.Equal(8, 2880 + 2560 - p.Bounds.Right); // 1x scale: an 8px gap
    }

    [Fact]
    public void Detached_keyboard_in_portrait_shows_the_content_centred_in_focus_mode()
    {
        var portrait = new MonitorInfo("surface", new Rect(0, 0, 1920, 2800), 2.0, true);
        var app = Win(1, portrait.WorkArea, active: true, maximized: true);
        var p = LayoutEngine.Place(Of(Posture.Tablet, [portrait], "surface", app), new PlacementRequest(new Size(1200, 900)));

        Assert.Equal(PlacementMethod.FocusMode, p.Method);
        Assert.Empty(p.Moves);
        var (cx, cy) = p.Bounds.Center;
        Assert.InRange(cx, 940, 980);
        Assert.InRange(cy, 1380, 1420);
    }

    [Fact]
    public void In_laptop_portrait_room_is_made_by_splitting_top_and_bottom()
    {
        var portrait = new MonitorInfo("surface", new Rect(0, 0, 1920, 2800), 2.0, true);
        var app = Win(1, portrait.WorkArea, active: true, maximized: true);
        var p = LayoutEngine.Place(Of(Posture.Laptop, [portrait], "surface", app), new PlacementRequest(new Size(900, 600)));

        Assert.Equal(PlacementMethod.MadeRoom, p.Method);
        var move = Assert.Single(p.Moves);
        Assert.Equal(portrait.WorkArea.Width - 32, move.To.Width); // full width, half the height
        Assert.True(move.To.Height < portrait.WorkArea.Height);
        Assert.False(p.Bounds.Intersects(move.To));
    }

    [Fact]
    public void A_window_that_cannot_be_moved_is_left_alone_and_the_engine_falls_back()
    {
        var stubborn = Win(1, Surface.WorkArea, active: true, maximized: true, canMove: false);
        var p = LayoutEngine.Place(Laptop(stubborn), new PlacementRequest(Chart));

        Assert.Equal(PlacementMethod.FocusMode, p.Method);
        Assert.Empty(p.Moves);
    }

    [Fact]
    public void Content_is_shrunk_a_little_before_any_fallback_is_tried()
    {
        var work = Win(1, new Rect(0, 0, 2150, 1840), active: true);
        // 2880 - 2150 = 730 px left, less the gaps: too narrow for 840 but fine at 80%.
        var p = LayoutEngine.Place(Laptop(work), new PlacementRequest(Chart));

        Assert.Equal(PlacementMethod.FreeSpaceShrunk, p.Method);
        Assert.True(p.Bounds.Width < Chart.Width);
        Assert.True(p.Bounds.Width >= Chart.Width * 0.7 - 1);
        Assert.False(p.Bounds.Intersects(work.Bounds));
    }

    [Fact]
    public void Minimized_and_other_desktop_windows_do_not_take_up_space()
    {
        var hidden = Win(1, new Rect(0, 0, 2880, 1840)) with { IsMinimized = true };
        var elsewhere = Win(2, new Rect(0, 0, 2880, 1840)) with { OnCurrentDesktop = false };
        var p = LayoutEngine.Place(Laptop(hidden, elsewhere), new PlacementRequest(Chart));
        Assert.Equal(PlacementMethod.FreeSpace, p.Method);
    }

    [Fact]
    public void An_anchor_preference_is_honoured_when_free_space_allows()
    {
        var p = LayoutEngine.Place(Laptop(), new PlacementRequest(new Size(440, 180), Anchor: Anchor.BottomLeft));
        Assert.Equal(16, p.Bounds.X);
        Assert.Equal(1840 - 16, p.Bounds.Bottom);
    }

    [Fact]
    public void Fallbacks_can_be_turned_off_or_reordered()
    {
        var app = Win(1, Surface.WorkArea, active: true, maximized: true);
        var options = new LayoutOptions { Fallbacks = [PlacementMethod.FocusMode] };
        var p = LayoutEngine.Place(Laptop(app), new PlacementRequest(Chart), options);
        Assert.Equal(PlacementMethod.FocusMode, p.Method);

        var none = LayoutEngine.Place(Laptop(app), new PlacementRequest(Chart), options with { Fallbacks = [] });
        Assert.Equal(PlacementMethod.FocusMode, none.Method);
    }

    [Fact]
    public void Gaps_follow_the_display_scale()
    {
        var s150 = new MonitorInfo("s", new Rect(0, 0, 2160, 1380), 1.5);
        var p = LayoutEngine.Place(Of(Posture.Laptop, [s150], "s"), new PlacementRequest(new Size(600, 400)));
        Assert.Equal(2160 - 12, p.Bounds.Right);
        Assert.Equal(Scaling.ToPhysical(8, 1.5), p.Bounds.Y);
        Assert.Equal(12.0, Scaling.ToDips(24, 2.0));
    }

    [Fact]
    public void Putting_it_back_restores_the_old_layout_exactly()
    {
        var app = Win(1, Surface.WorkArea, active: true, maximized: true);
        var placement = LayoutEngine.Place(Laptop(app), new PlacementRequest(Chart));
        var history = new LayoutHistory();
        history.Record(placement);

        var moved = app with { Bounds = placement.Moves[0].To };
        var restore = history.PutBack([moved]);
        Assert.NotNull(restore);
        var back = Assert.Single(restore!.Moves);
        Assert.Equal(app.Bounds, back.To);
        Assert.Empty(restore.Skipped);
        Assert.Null(history.PutBack([moved])); // nothing left
    }

    [Fact]
    public void Put_back_does_not_fight_a_window_the_person_moved_themselves()
    {
        var app = Win(1, Surface.WorkArea, active: true, maximized: true);
        var placement = LayoutEngine.Place(Laptop(app), new PlacementRequest(Chart));
        var history = new LayoutHistory();
        history.Record(placement);

        var draggedByHand = app with { Bounds = new Rect(100, 100, 800, 600) };
        var restore = history.PutBack([draggedByHand])!;
        Assert.Empty(restore.Moves);
        Assert.Equal([1L], restore.Skipped);
    }

    [Fact]
    public void Habits_are_suggested_only_after_repeated_choices_and_only_once()
    {
        var tracker = new HabitTracker();
        var area = Surface.WorkArea;
        var rightHalf = new Rect(1444, 0, 1436, 1840);
        Assert.Null(tracker.Record("PDFs", rightHalf, area));
        Assert.Null(tracker.Record("PDFs", rightHalf, area));
        var suggestion = tracker.Record("PDFs", rightHalf, area);
        Assert.NotNull(suggestion);
        Assert.Equal(Region.RightHalf, suggestion!.Region);
        Assert.Equal("Open PDFs on the right half from now on?", suggestion.Message);
        Assert.Null(tracker.Record("PDFs", rightHalf, area));
    }

    [Fact]
    public void Free_space_is_exact_for_simple_arrangements()
    {
        var area = new Rect(0, 0, 100, 100);
        Assert.Equal([area], FreeSpace.MaximalEmptyRects(area, []));

        // A block in the middle leaves four overlapping maximal strips: left, right, top, bottom.
        var rects = FreeSpace.MaximalEmptyRects(area, [new Rect(40, 40, 20, 20)]);
        Assert.Equal(4, rects.Count);
        Assert.Contains(new Rect(0, 0, 40, 100), rects);
        Assert.Contains(new Rect(60, 0, 40, 100), rects);
        Assert.Contains(new Rect(0, 0, 100, 40), rects);
        Assert.Contains(new Rect(0, 60, 100, 40), rects);

        Assert.Empty(FreeSpace.MaximalEmptyRects(area, [area]));
    }

    [Fact]
    public void Placement_stays_fast_with_a_crowded_desktop()
    {
        var windows = Enumerable.Range(0, 40)
            .Select(i => Win(i + 1, new Rect(i * 61 % 2400, i * 47 % 1500, 380 + i % 5 * 40, 300 + i % 3 * 50), active: i == 7))
            .ToArray();
        var desktop = Laptop(windows);
        LayoutEngine.Place(desktop, new PlacementRequest(Chart)); // warm up
        var clock = Stopwatch.StartNew();
        var p = LayoutEngine.Place(desktop, new PlacementRequest(Chart));
        clock.Stop();
        Assert.True(clock.ElapsedMilliseconds < 100, $"took {clock.ElapsedMilliseconds} ms");
        Assert.True(Surface.WorkArea.Contains(p.Bounds));
    }
}
