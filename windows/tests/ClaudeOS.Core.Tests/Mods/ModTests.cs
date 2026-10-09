using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Design;
using ClaudeOS.Core.Layout;
using Size = ClaudeOS.Core.Layout.Size;
using ClaudeOS.Core.Mods;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests.Mods;

public sealed class ModTests : IDisposable
{
    private const string Battery = """
        {
          "id": "battery-and-next-meeting",
          "name": "Battery and next meeting",
          "version": "0.1.0",
          "kind": "widget",
          "level": "declarative",
          "placement": { "anchor": "top-right", "size": [220, 90] },
          "capabilities": ["system.battery", "calendar.read.next"],
          "view": {
            "stack": [
              { "metric": "{system.battery.percent}%", "label": "Battery" },
              { "text": "{calendar.next.title}", "subtext": "{calendar.next.startsIn}" }
            ]
          },
          "settings": {
            "accent": { "type": "color", "default": "#7aa2f7", "label": "Accent" },
            "showSeconds": { "type": "boolean", "default": "false" }
          }
        }
        """;

    private readonly TestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private sealed class MapProvider(string prefix, params (string Path, string Value)[] values) : IDataProvider
    {
        public string Prefix => prefix;

        public bool TryGet(string path, out string value)
        {
            value = values.FirstOrDefault(v => v.Path == path).Value ?? "";
            return value.Length > 0;
        }
    }

    private static readonly IDataProvider[] Sample =
    [
        new MapProvider("system.battery", ("system.battery.percent", "82")),
        new MapProvider("calendar.next", ("calendar.next.title", "Design review"), ("calendar.next.startsIn", "in 25 min")),
    ];

    private static ModManifest Parse(string json) => ModManifest.Parse(json);

    [Fact]
    public void The_example_widget_from_the_plan_parses_with_its_settings()
    {
        var m = Parse(Battery);
        Assert.Equal((ModKind.Widget, ModLevel.Declarative), (m.Kind, m.Level));
        Assert.Equal(new WidgetPlacement("top-right", 220, 90), m.Placement);
        Assert.Equal(["system.battery", "calendar.read.next"], m.Capabilities.ToArray());
        Assert.Equal(ViewKind.Stack, m.View!.Kind);
        Assert.Equal(2, m.View.Children.Length);
        Assert.Equal(["calendar.next.startsIn", "calendar.next.title", "system.battery.percent"], m.View.Paths().Order().ToArray());
        Assert.Equal([SettingType.Color, SettingType.Boolean], m.Settings.Select(s => s.Type).ToArray());
    }

    [Theory]
    [InlineData("""{"id":"Bad_ID","name":"x","kind":"widget"}""", "lowercase words")]
    [InlineData("""{"id":"x","name":"x","kind":"app"}""", "kind 'app'")]
    [InlineData("""{"id":"x","name":"x","kind":"command","exec":"calc.exe"}""", "unknown mod.json fields: exec")]
    [InlineData("""{"id":"x","name":"x","kind":"command","capabilities":["filesystem.everything"]}""", "unknown capability")]
    [InlineData("""{"id":"x","name":"x","kind":"command","capabilities":["network.fetch"]}""", "unknown capability")]
    [InlineData("""{"id":"x","name":"x","kind":"command","capabilities":["network.fetch:evil.example/path"]}""", "unknown capability")]
    [InlineData("""{"id":"x","name":"x","kind":"widget","placement":{"size":[10,10]},"view":{"text":"hi"}}""", "between 80x40")]
    [InlineData("""{"id":"x","name":"x","kind":"widget","view":{"text":"hi"}}""", "needs both a view and a placement")]
    [InlineData("""{"id":"x","name":"x","kind":"widget","placement":{"size":[100,100]},"view":{"text":"a","metric":"b"}}""", "exactly one of")]
    [InlineData("""{"id":"x","name":"x","kind":"widget","placement":{"size":[100,100]},"view":{"text":"a","onclick":"evil()"}}""", "does not take: onclick")]
    [InlineData("""{"id":"x","name":"x","kind":"theme"}""", "needs a theme block")]
    [InlineData("nope", "not valid JSON")]
    public void Manifests_are_validated_strictly(string json, string expected) =>
        Assert.Contains(expected, Assert.Throws<ModException>(() => Parse(json)).Message);

    [Fact]
    public void Views_are_bounded_in_depth_and_size()
    {
        var deep = "{\"text\":\"x\"}";
        for (var i = 0; i < 8; i++)
        {
            deep = $"{{\"stack\":[{deep}]}}";
        }

        var json = """{"id":"x","name":"x","kind":"widget","placement":{"size":[100,100]},"view":__VIEW__}""".Replace("__VIEW__", deep);
        Assert.Contains("nested at most", Assert.Throws<ModException>(() => Parse(json)).Message);

        var wide = string.Join(",", Enumerable.Repeat("{\"text\":\"x\"}", 70));
        var json2 = """{"id":"x","name":"x","kind":"widget","placement":{"size":[100,100]},"view":{"stack":[__WIDE__]}}""".Replace("__WIDE__", wide);
        Assert.Contains("at most 60 elements", Assert.Throws<ModException>(() => Parse(json2)).Message);
    }

    [Fact]
    public void The_capabilities_are_described_in_plain_words_with_a_live_preview()
    {
        var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
        var proposal = store.Review(Battery, new ViewBinder(new CapabilityBroker(), Sample));

        Assert.Equal(["See your battery level and whether it is charging", "Read the title and start time of your next meeting"], proposal.Capabilities.Select(c => c.Description));
        Assert.Equal(Risk.Medium, proposal.Capabilities.Max(c => c.Risk));
        Assert.Equal("82%", proposal.Preview!.Children[0].Primary);
        Assert.Equal("Design review", proposal.Preview.Children[1].Primary);
        Assert.Equal("in 25 min", proposal.Preview.Children[1].Secondary);
        Assert.Equal("battery-and-next-meeting/mod.json", Assert.IsType<WriteFile>(proposal.Plan.Actions[0]).Path);
    }

    [Fact]
    public void A_view_cannot_read_data_its_manifest_does_not_declare()
    {
        var sneaky = Battery.Replace("\"capabilities\": [\"system.battery\", \"calendar.read.next\"]", "\"capabilities\": [\"system.battery\"]");
        var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
        var e = Assert.Throws<ModException>(() => store.Review(sneaky));
        Assert.Contains("calendar.next.startsIn", e.Message);
        Assert.Contains("does not declare a capability", e.Message);
    }

    [Fact]
    public void The_binder_checks_the_grant_on_every_read_even_if_the_manifest_changed_later()
    {
        var broker = new CapabilityBroker();
        broker.Grant("battery-and-next-meeting", ["system.battery"]); // calendar was never approved
        var binder = new ViewBinder(broker, Sample);
        var e = Assert.Throws<CapabilityDeniedException>(() => binder.Bind(Parse(Battery)));
        Assert.Contains("calendar.next.title", e.Message);

        broker.Grant("battery-and-next-meeting", ["system.battery", "calendar.read.next"]);
        Assert.Equal("Design review", binder.Bind(Parse(Battery)).Children[1].Primary); // 'calendar.read.next' unlocks 'calendar.next.*'
    }

    [Fact]
    public void Settings_flow_into_the_view_and_defaults_apply()
    {
        const string json = """
            {"id":"clock","name":"Clock","kind":"widget","placement":{"size":[160,60]},"capabilities":["system.time"],
             "view":{"stack":[{"metric":"{system.time.hour}","label":"{settings.label}"}]},
             "settings":{"label":{"type":"text","default":"Now"}}}
            """;
        var broker = new CapabilityBroker();
        broker.Grant("clock", ["system.time"]);
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 9, 14, 5, 0, TimeSpan.Zero));
        var binder = new ViewBinder(broker, [new ClockProvider(clock)]);

        Assert.Equal("Now", binder.Bind(Parse(json)).Children[0].Secondary);
        Assert.Equal("Local", binder.Bind(Parse(json), new Dictionary<string, string> { ["label"] = "Local" }).Children[0].Secondary);
        Assert.Matches(@"^\d{1,2}:05$", binder.Bind(Parse(json)).Children[0].Primary);
    }

    [Fact]
    public async Task Installing_goes_through_the_normal_approval_and_editing_the_mod_needs_review_again()
    {
        var modsRoot = Path.Combine(_ws.Base, "mods");
        Directory.CreateDirectory(modsRoot);
        var broker = new CapabilityBroker();
        var store = new ModStore(modsRoot, broker);
        var proposal = store.Review(Battery);

        var outcome = await Session.ReviewAndApplyAsync(proposal.Plan, new Overlay(modsRoot), new Policy(), (_, _) => Task.FromResult(true), new OutboxConnector(_ws.State.Outbox), _ws.State);
        Assert.Equal(Session.Status.Applied, outcome.Status);
        store.RecordApproval(proposal.Manifest, proposal.ManifestJson);

        var loaded = Assert.Single(store.LoadAll());
        Assert.Equal(ModStatus.Active, loaded.Status);
        Assert.True(broker.Allows("battery-and-next-meeting", "system.battery.percent"));

        // The mod is edited on disk to ask for more. The old approval does not cover it.
        var path = Path.Combine(modsRoot, "battery-and-next-meeting", "mod.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"calendar.read.next\"", "\"calendar.read.next\", \"files.recent\""));
        var fresh = new CapabilityBroker();
        var after = Assert.Single(new ModStore(modsRoot, fresh).LoadAll());
        Assert.Equal(ModStatus.NeedsReview, after.Status);
        Assert.False(fresh.Allows("battery-and-next-meeting", "system.battery.percent"));

        // Disabling is one call and revokes access; the install itself is undoable.
        File.WriteAllText(path, proposal.ManifestJson);
        var s2 = new ModStore(modsRoot, broker);
        s2.SetEnabled("battery-and-next-meeting", false);
        Assert.Equal(ModStatus.Disabled, Assert.Single(s2.LoadAll()).Status);
        Assert.False(broker.Allows("battery-and-next-meeting", "system.battery.percent"));

        Overlay.Undo(outcome.Result!.Manifest!);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Scripted_and_web_mods_are_refused_honestly_until_they_exist()
    {
        var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
        var json = """{"id":"x","name":"X","kind":"command","level":"scripted","commands":[{"phrase":"p","steps":["open a"]}]}""";
        Assert.Contains("not available yet", Assert.Throws<ModException>(() => store.Review(json)).Message);
    }

    [Fact]
    public void A_theme_mod_becomes_checked_overrides_and_cannot_hide_risk()
    {
        var good = Parse("""{"id":"ocean","name":"Ocean","kind":"theme","theme":{"accent":"#2A78D6","radiusScale":1.4,"density":"compact","backdrop":"acrylic"}}""");
        var theme = ThemeResolver.Resolve(Appearance.Dark, ModEffects.ToThemeOverrides(good));
        Assert.True(theme.IsValid, string.Join("; ", theme.Errors));
        Assert.Equal(Density.Compact, theme.Density);
        Assert.Equal(Backdrop.Acrylic, theme.Backdrop);

        var bad = Parse("""{"id":"calm-risk","name":"Calm risk","kind":"theme","theme":{"colors":{"diff.remove":"#00AA00"}}}""");
        var refused = ThemeResolver.Resolve(Appearance.Light, ModEffects.ToThemeOverrides(bad));
        Assert.False(refused.IsValid);
    }

    [Fact]
    public void Layout_rule_mods_change_where_content_lands()
    {
        var rule = Parse("""{"id":"charts-bottom-right","name":"Charts bottom right","kind":"layout-rule","rules":[{"match":{"kind":"chart"},"place":{"anchor":"bottom-right","size":[420,280]}}]}""");
        var request = ModEffects.ApplyRules([rule], "chart", new PlacementRequest(new Size(800, 500)));
        Assert.Equal(Anchor.BottomRight, request.Anchor);
        Assert.Equal(new Size(420, 280), request.Desired);
        Assert.Equal(Anchor.Auto, ModEffects.ApplyRules([rule], "report", new PlacementRequest(new Size(800, 500))).Anchor);
    }

    [Fact]
    public void Command_mods_add_phrases_the_router_understands_before_any_model()
    {
        var standup = Parse("""{"id":"standup","name":"Standup","kind":"command","commands":[{"phrase":"Standup","steps":["open standup notes","snap left"]}]}""");
        var hit = ModEffects.MatchCommand([standup], "  standup! ");
        Assert.NotNull(hit);
        Assert.Equal(["open standup notes", "snap left"], hit!.Steps.ToArray());
        Assert.Null(ModEffects.MatchCommand([standup], "standup now please"));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
