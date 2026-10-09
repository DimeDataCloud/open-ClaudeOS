using ClaudeOS.Core.Intent;

namespace ClaudeOS.Core.Tests.Intent;

/// <summary>
/// The on-device classifier is measured on phrases it was never trained on, in two sets. The
/// <em>development</em> phrases were used to choose how cautious it is (the temperature and the 0.8
/// threshold). The <em>fresh</em> phrases were written afterwards and looked at only once, so they are
/// the honest estimate. A test checks that neither set leaked into the training phrases.
/// </summary>
public sealed class LocalIntentClassifierTests
{
    private static readonly LocalIntentClassifier Classifier = new();

    private static readonly (IntentLabel Label, string Phrase)[] Development =
    [
        .. Set(IntentLabel.Open,
            "i'd like to see the annual report", "pull up the vendor contract for me", "can you get me last year's tax return",
            "let me see the project timeline file", "i want to look at the wedding photos", "bring me the proposal draft",
            "the lease, please", "i need the employee handbook", "get my cv up", "go to the shared drive folder",
            "let me read the privacy policy doc", "my insurance paperwork please", "i want to check the sales forecast spreadsheet"),
        .. Set(IntentLabel.Find,
            "where did that invoice from last spring end up", "any document that mentions the merger", "search for files about the apartment lease",
            "i need to track down my old passport photo", "which file has the vpn instructions", "is there anything on the kickoff in my documents",
            "locate the folder with last year's taxes", "hunt for the spreadsheet with the headcount", "do i have anything from the landlord",
            "what files did i open yesterday", "i misplaced the signed nda", "look for pictures from the conference"),
        .. Set(IntentLabel.Make,
            "turn the sales sheet into a picture", "put together a short write up of today's standup", "build a widget that shows cpu usage",
            "i want a graph of monthly signups", "create a little dashboard for my habits", "draw the flow of how orders get processed",
            "give me a summary of these meeting notes", "make a table of the top ten customers", "a theme with warmer colours",
            "visualise the support tickets by week", "come up with a timeline for the launch", "whip up a report on the survey results",
            "generate a chart of churn by quarter", "i need a visual breakdown of the budget"),
        .. Set(IntentLabel.Change,
            "the axis text is hard to read", "make the line thicker", "can you drop the legend", "put the title in the centre",
            "use a lighter blue", "show the values on top of the bars", "sort these from high to low", "change the heading to monthly spend",
            "i'd prefer bars over the line", "enlarge the numbers", "get rid of the grid", "only the last six months please"),
        .. Set(IntentLabel.Window,
            "put this next to my browser", "i need this on the right side of the screen", "make everything fit side by side",
            "send this over to the other display", "expand this to fill the monitor", "shove this window out of the way",
            "arrange my windows in a grid", "bring everything back where it was", "make this thing smaller"),
        .. Set(IntentLabel.Other,
            "what is the population of canada", "can you tell me a bedtime story", "email the invoices to my accountant",
            "remind me about the dentist tomorrow", "who is the president of france", "move all my screenshots into a folder",
            "what's the best way to learn spanish", "delete duplicate photos", "how do i boil an egg",
            "send a message to mark saying i'm late", "turn the volume up", "write me a haiku about rain", "how far is the moon",
            "compress my downloads folder"),
    ];


    private static readonly (IntentLabel Label, string Phrase)[] Fresh =
    [
        .. Set(IntentLabel.Open,
            "i'd like the april budget on screen", "get my cover letter", "could you bring up the client contract",
            "i want to review the meeting minutes", "let me glance at the invoice from globex", "the product spec, please",
            "i need the slides from friday", "take me to the vacation photos", "get the signed lease up",
            "i'd like to read the research paper i saved", "load my budget spreadsheet", "can you bring back the draft i was working on"),
        .. Set(IntentLabel.Find,
            "where is the receipt for the laptop", "which of my files talk about the reorg", "i can't find the slides from friday",
            "any pdf containing the word indemnity", "search my documents for the wifi router manual", "track down last month's electricity bill",
            "is there a file about the roof repair", "what did sarah send me about the budget", "look for anything tagged urgent",
            "find me the folder i used for the move", "dig through my files for the old logo", "which spreadsheet has the hiring plan"),
        .. Set(IntentLabel.Make,
            "i need a bar picture of sales by month", "can you put together a briefing on the competitor", "create a tiny widget showing the date",
            "turn this csv into a graph", "draft a summary of the board meeting", "build a cheat sheet from these notes",
            "sketch a diagram of the checkout process", "make me a table of overdue invoices", "give me a rounded dark theme",
            "plot the temperature readings over time", "a one page overview of the project status", "produce a visual of customer growth"),
        .. Set(IntentLabel.Change,
            "the bars should be orange", "make the title bigger", "can we see the numbers on each bar", "remove the border",
            "order them alphabetically", "set the font to something lighter", "make the chart wider", "show percentages instead",
            "i don't like the grid lines", "put the legend underneath", "limit it to the top three", "rename the axis to revenue"),
        .. Set(IntentLabel.Window,
            "snap this beside the other one", "i want this to cover the left half", "move this to my other monitor",
            "make this fill the screen", "get this window out of the way for now", "put these windows next to each other",
            "reset my window layout", "make this a bit narrower", "stay on top of everything please"),
        .. Set(IntentLabel.Other,
            "what's the tallest mountain in the world", "sing me a song", "email last quarter's report to the team", "order a pizza",
            "rename my photos by date taken", "how long does it take to fly to london", "move the old invoices to an archive folder",
            "what is photosynthesis", "remind me to water the plants", "delete everything in the trash",
            "translate this paragraph into german", "text my brother that i'm on my way"),
    ];

    private static IEnumerable<(IntentLabel, string)> Set(IntentLabel label, params string[] phrases) => phrases.Select(p => (label, p));

    [Fact]
    public void Neither_test_set_leaked_into_the_training_phrases()
    {
        var trained = IntentTraining.Examples.Select(e => e.Phrase.ToLowerInvariant()).ToHashSet();
        Assert.All(Development, h => Assert.DoesNotContain(h.Phrase.ToLowerInvariant(), trained));
        Assert.All(Fresh, h => Assert.DoesNotContain(h.Phrase.ToLowerInvariant(), trained));
        Assert.Empty(Fresh.Select(f => f.Phrase).Intersect(Development.Select(d => d.Phrase)));
        Assert.True(IntentTraining.Examples.Count >= 150);
        Assert.All(Enum.GetValues<IntentLabel>(), label => Assert.True(IntentTraining.Examples.Count(e => e.Label == label) >= 18, label.ToString()));
    }

    private sealed record Measurement(int Total, int Top1, int Accepted, int AcceptedRight, List<string> Wrong)
    {
        public override string ToString() =>
            $"top-1 {Top1}/{Total}; accepted at 0.8: {Accepted}/{Total}, of which right {AcceptedRight}; wrongly accepted: [{string.Join(" | ", Wrong)}]";
    }

    private static Measurement Measure((IntentLabel Label, string Phrase)[] set)
    {
        var top1 = 0;
        var accepted = 0;
        var acceptedRight = 0;
        var wrong = new List<string>();
        foreach (var (label, phrase) in set)
        {
            var r = Classifier.Classify(phrase);
            top1 += r.Label == label ? 1 : 0;
            if (r.Confidence < 0.8)
            {
                continue;
            }

            accepted++;
            if (r.Label == label)
            {
                acceptedRight++;
            }
            else
            {
                wrong.Add($"{phrase} => {r.Label} {r.Confidence:0.00} (wanted {label})");
            }
        }

        return new Measurement(set.Length, top1, accepted, acceptedRight, wrong);
    }

    [Fact]
    public void On_phrases_it_never_saw_it_is_right_when_it_answers_and_silent_when_unsure()
    {
        // The fresh set was written after the operating point was chosen and looked at once; these
        // bounds guard the numbers it gave, with a little room, so a change that makes it reckless fails.
        var fresh = Measure(Fresh);
        Assert.True(fresh.Top1 >= Fresh.Length * 0.8, fresh.ToString());
        Assert.True(fresh.Accepted >= Fresh.Length * 0.35, fresh.ToString());
        Assert.True(fresh.AcceptedRight >= fresh.Accepted * 0.92, fresh.ToString());

        var development = Measure(Development);
        Assert.True(development.Top1 >= Development.Length * 0.8, development.ToString());
        Assert.True(development.AcceptedRight >= development.Accepted * 0.95, development.ToString());
    }

    [Fact]
    public void Chat_and_tasks_for_the_planner_are_never_confidently_taken_for_a_command()
    {
        var misrouted = Development.Concat(Fresh)
            .Where(h => h.Label == IntentLabel.Other)
            .Select(h => (h.Phrase, Result: Classifier.Classify(h.Phrase)))
            .Where(x => x.Result.Confidence >= 0.8 && x.Result.Label is IntentLabel.Open or IntentLabel.Find or IntentLabel.Make or IntentLabel.Change)
            .Select(x => $"{x.Phrase} => {x.Result.Label} {x.Result.Confidence:0.00}")
            .ToList();
        Assert.True(misrouted.Count == 0, string.Join(" | ", misrouted));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("zxqv blorp wibble")]
    [InlineData("!!!???")]
    [InlineData("日本語のテキスト")]
    public void Words_it_has_never_seen_get_no_confidence_at_all(string text) => Assert.Equal(0, Classifier.Classify(text).Confidence);

    [Fact]
    public void Mostly_unknown_words_lower_the_confidence_of_the_few_it_knows()
    {
        var plain = Classifier.Classify("can you get me last year's tax return");
        var noisy = Classifier.Classify("can you get me last year's tax return zxqv blorp wibble flarp snorf glorp");
        Assert.True(noisy.Confidence < plain.Confidence);
        Assert.True(noisy.Confidence < 0.8, $"{noisy}");
    }

    [Fact]
    public void It_is_deterministic_and_fast()
    {
        var first = Classifier.Classify("put this next to my browser");
        Assert.Equal(first, Classifier.Classify("put this next to my browser"));

        var best = TimeSpan.MaxValue;
        for (var run = 0; run < 5; run++)
        {
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (var i = 0; i < 200; i++)
            {
                Classifier.Classify("i'd like to see the annual report from last year");
            }

            var each = System.Diagnostics.Stopwatch.GetElapsedTime(start) / 200;
            best = each < best ? each : best;
        }

        Assert.True(best < TimeSpan.FromMilliseconds(2), $"best of 5: {best.TotalMilliseconds:0.000} ms per phrase");
    }

    [Fact]
    public void Random_text_never_throws_and_confidence_stays_a_probability()
    {
        var rng = new Random(20261012);
        for (var i = 0; i < 3000; i++)
        {
            var chars = new char[rng.Next(0, 80)];
            for (var j = 0; j < chars.Length; j++)
            {
                chars[j] = (char)rng.Next(32, rng.Next(2) == 0 ? 127 : 0x2FFF);
            }

            var r = Classifier.Classify(new string(chars));
            Assert.InRange(r.Confidence, 0, 1);
        }
    }

    [Fact]
    public async Task The_router_uses_it_between_the_grammar_and_the_cloud()
    {
        var router = new IntentRouter(Classifier);

        // The grammar knows this phrasing, so the classifier is never asked.
        Assert.Equal(RouteSource.Grammar, (await router.RouteAsync("open the budget")).Source);

        // A phrasing the grammar does not know is understood on the device, with no tokens spent.
        var routed = await router.RouteAsync("i need a little widget for my battery level");
        Assert.Equal(RouteSource.Npu, routed.Source);
        var make = Assert.IsType<MakeIntent>(routed.Intent);
        Assert.Equal(MakeKind.Widget, make.Kind);

        // Chat is not taken for a command: with nothing else configured it stays unclear.
        var chat = await router.RouteAsync("what is the population of canada");
        Assert.IsType<UnclearIntent>(chat.Intent);
    }

    [Theory]
    [InlineData("i need the employee handbook", "employee handbook")]
    [InlineData("can you get me last year's tax return", "last year's tax return")]
    [InlineData("i'd like to see the annual report", "annual report")]
    [InlineData("search my documents for the wifi router manual", "wifi router manual")]
    [InlineData("any pdf containing the word indemnity", "pdf word indemnity")]
    [InlineData("please", "")]
    public void The_query_keeps_the_words_that_name_the_thing(string text, string expected) => Assert.Equal(expected, IntentGrammar.QueryOf(text));

    [Fact]
    public async Task A_request_with_nothing_to_search_for_goes_on_to_the_cloud_instead_of_searching_for_nothing()
    {
        var router = new IntentRouter(new FixedLabel(IntentLabel.Open), new Cloud());
        var routed = await router.RouteAsync("could you please");
        Assert.Equal(RouteSource.Cloud, routed.Source);

        var named = await router.RouteAsync("i need the employee handbook");
        Assert.Equal(RouteSource.Npu, named.Source);
        Assert.Equal("employee handbook", Assert.IsType<OpenIntent>(named.Intent).Query);
    }

    private sealed class FixedLabel(IntentLabel label) : IIntentClassifier
    {
        public Task<ClassifierResult> ClassifyAsync(string text, CancellationToken ct = default) => Task.FromResult(new ClassifierResult(label, 0.99));
    }

    private sealed class Cloud : ICloudIntentResolver
    {
        public Task<UserIntent> ResolveAsync(string text, CancellationToken ct = default) => Task.FromResult<UserIntent>(new UnclearIntent(text));
    }
}
