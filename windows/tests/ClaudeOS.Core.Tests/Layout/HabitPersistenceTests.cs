using ClaudeOS.Core.Layout;

namespace ClaudeOS.Core.Tests.Layout;

public sealed class HabitPersistenceTests
{
    private static readonly Rect Area = new(0, 0, 2880, 1840);
    private static readonly Rect TopRight = new(2200, 40, 640, 420);
    private static readonly Rect LeftHalf = new(0, 0, 1436, 1840);

    [Fact]
    public void A_restart_does_not_forget_a_habit_in_progress()
    {
        var before = new HabitTracker();
        Assert.Null(before.Record("chart", TopRight, Area));
        Assert.Null(before.Record("chart", TopRight, Area));

        var after = HabitTracker.Load(before.Save());
        var offer = after.Record("chart", TopRight, Area);
        Assert.NotNull(offer);
        Assert.Equal(Region.TopRight, offer!.Region);
    }

    [Fact]
    public void An_offer_that_was_made_is_never_made_again_after_a_restart()
    {
        var before = new HabitTracker();
        for (var i = 0; i < 3; i++)
        {
            before.Record("chart", TopRight, Area);
        }

        var after = HabitTracker.Load(before.Save());
        for (var i = 0; i < 10; i++)
        {
            Assert.Null(after.Record("chart", TopRight, Area));
        }

        // A different habit is still learned.
        Assert.Null(after.Record("chart", LeftHalf, Area));
        Assert.Null(after.Record("chart", LeftHalf, Area));
        Assert.NotNull(after.Record("chart", LeftHalf, Area));
    }

    [Fact]
    public void Saving_is_stable_and_round_trips()
    {
        var tracker = new HabitTracker();
        tracker.Record("widget", TopRight, Area);
        tracker.Record("chart", LeftHalf, Area);
        tracker.Record("chart", LeftHalf, Area);
        var first = tracker.Save();
        Assert.Equal(first, HabitTracker.Load(first).Save());
        Assert.Contains("\"chart\"", first);
        Assert.DoesNotContain("\n", first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"counts\":")]
    [InlineData("{\"counts\":\"x\",\"offered\":5}")]
    [InlineData("{\"counts\":[{\"kind\":\"chart\",\"region\":\"Nowhere\",\"n\":9}]}")]
    [InlineData("{\"counts\":[{\"kind\":\"chart\",\"region\":\"TopRight\",\"n\":-3}]}")]
    [InlineData("{\"counts\":[{\"kind\":\"chart\",\"region\":\"TopRight\",\"n\":99999999999999}]}")]
    [InlineData("{\"counts\":[{\"kind\":7,\"region\":\"TopRight\",\"n\":1}]}")]
    [InlineData("{\"counts\":[null,1,\"a\",[]],\"offered\":[{}]}")]
    public void A_damaged_file_gives_a_fresh_tracker_not_an_error(string? json)
    {
        var tracker = HabitTracker.Load(json);
        Assert.Null(tracker.Record("chart", TopRight, Area));
        Assert.Null(tracker.Record("chart", TopRight, Area));
        Assert.NotNull(tracker.Record("chart", TopRight, Area));
    }

    [Fact]
    public void A_hand_edited_count_cannot_be_used_to_flood_or_to_force_an_offer_of_nonsense()
    {
        var huge = new string('x', 100_000);
        Assert.Null(HabitTracker.Load("{\"counts\":[{\"kind\":\"" + huge + "\",\"region\":\"TopRight\",\"n\":5}]}").Record(huge[..40], TopRight, Area));

        var longName = new string('k', 41);
        var tracker = HabitTracker.Load("{\"counts\":[{\"kind\":\"" + longName + "\",\"region\":\"TopRight\",\"n\":50}]}");
        Assert.Null(tracker.Record(longName, TopRight, Area)); // an over-long kind was dropped, so this is the first sighting, not the fiftieth
    }

    [Fact]
    public void Random_bytes_never_throw()
    {
        var rng = new Random(20261011);
        for (var i = 0; i < 2000; i++)
        {
            var bytes = new byte[rng.Next(0, 200)];
            rng.NextBytes(bytes);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            _ = HabitTracker.Load(text);
            _ = HabitTracker.Load("{\"counts\":[" + text + "]}");
        }
    }
}
