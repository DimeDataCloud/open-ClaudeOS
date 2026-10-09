using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Mods;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests.Mods;

/// <summary>The scripted level: formulas that can only calculate from data a person approved.</summary>
public sealed class ScriptedModTests : IDisposable
{
    private readonly TestWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    private static FormulaValue Run(string source, params (string Name, string Value)[] names)
    {
        var map = names.ToDictionary(n => n.Name, n => n.Value);
        return Formula.Parse(source).Evaluate(n => map.TryGetValue(n, out var v) ? FormulaValue.FromData(v) : FormulaValue.Missing, new FormulaBudget(FormulaBudget.PerWidget));
    }

    private static string Show(string source, params (string Name, string Value)[] names) => Run(source, names).Display;

    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("(1 + 2) * 3", "9")]
    [InlineData("10 - 4 - 3", "3")]
    [InlineData("7 % 4", "3")]
    [InlineData("-5 + 2", "-3")]
    [InlineData("1 / 4", "0.25")]
    [InlineData("0.1 + 0.2", "0.3")]
    [InlineData("\"a\" + \"b\" + 1", "ab1")]
    [InlineData("'single' + \" and double\"", "single and double")]
    [InlineData("1 < 2 && 2 < 3", "true")]
    [InlineData("1 > 2 || !(2 > 3)", "true")]
    [InlineData("\"abc\" < \"abd\"", "true")]
    [InlineData("1 == 1.0", "true")]
    [InlineData("\"x\" != \"y\"", "true")]
    public void Operators_work_the_way_they_read(string formula, string expected) => Assert.Equal(expected, Show(formula));

    [Theory]
    [InlineData("round(2.5)", "3")]
    [InlineData("round(-2.5)", "-3")]
    [InlineData("round(3.14159, 2)", "3.14")]
    [InlineData("floor(2.9) + ceil(2.1)", "5")]
    [InlineData("abs(-4)", "4")]
    [InlineData("min(3, 1, 2)", "1")]
    [InlineData("max(3, 1, 2)", "3")]
    [InlineData("clamp(150, 0, 100)", "100")]
    [InlineData("upper(\"ab\") + lower(\"CD\")", "ABcd")]
    [InlineData("trim(\"  hi \")", "hi")]
    [InlineData("len(\"hello\")", "5")]
    [InlineData("left(\"hello\", 2) + right(\"hello\", 2)", "helo")]
    [InlineData("left(\"hi\", 99)", "hi")]
    [InlineData("pad(7, 2)", "07")]
    [InlineData("contains(\"Design Review\", \"review\")", "true")]
    [InlineData("startsWith(\"Design\", \"des\")", "true")]
    [InlineData("endsWith(\"Design\", \"x\")", "false")]
    [InlineData("number(\"42\") + 1", "43")]
    [InlineData("text(42) + \"!\"", "42!")]
    [InlineData("if(1 > 2, \"a\", \"b\")", "b")]
    [InlineData("coalesce(missing.thing, \"fallback\")", "fallback")]
    public void The_functions_do_what_they_say(string formula, string expected) => Assert.Equal(expected, Show(formula));

    [Fact]
    public void Data_arrives_typed_and_a_missing_reading_shows_a_dash_instead_of_failing()
    {
        Assert.Equal("80", Show("round(system.battery.percent / 10) * 10", ("system.battery.percent", "82")));
        Assert.Equal("Low", Show("if(system.battery.percent < 20, \"Low\", \"OK\")", ("system.battery.percent", "12")));
        Assert.Equal("OK", Show("if(system.battery.percent < 20, \"Low\", \"OK\")", ("system.battery.percent", "55")));
        Assert.Equal("—", Show("system.battery.percent + 1"));
        Assert.Equal("—", Show("round(system.battery.percent)"));
        Assert.Equal("—", Show("upper(system.battery.title)"));
        Assert.Equal("false", Show("system.battery.percent < 20"));
        Assert.Equal("true", Show("system.battery.percent == system.other"));
        Assert.Equal("yes", Show("if(system.battery.charging, \"yes\", \"no\")", ("system.battery.charging", "true")));
        Assert.Equal("no", Show("if(system.battery.charging, \"yes\", \"no\")", ("system.battery.charging", "false")));
    }

    [Fact]
    public void Dividing_by_zero_is_a_dash_not_a_crash_or_infinity()
    {
        Assert.Equal("—", Show("1 / 0"));
        Assert.Equal("—", Show("5 % 0"));
        Assert.Equal("—", Show("clamp(5, 10, 1)"));
    }

    [Fact]
    public void Only_the_chosen_branch_is_read()
    {
        var reads = new List<string>();
        var formula = Formula.Parse("if(a, b, c)");
        formula.Evaluate(n => { reads.Add(n); return n == "a" ? FormulaValue.Of(true) : FormulaValue.Of(1); }, new FormulaBudget(100));
        Assert.Equal(["a", "b"], reads);
    }

    [Theory]
    [InlineData("1 +", "ends where a value was expected")]
    [InlineData("(1 + 2", "missing ')'")]
    [InlineData("1 2", "unexpected '2'")]
    [InlineData("\"open", "closing quote")]
    [InlineData("\"bad \\q\"", "is not an escape")]
    [InlineData("12abc", "cannot run straight into letters")]
    [InlineData("a..b", "not a valid name")]
    [InlineData("a.", "not a valid name")]
    [InlineData("system.time.now()", "is a path, not a function")]
    [InlineData("exec(\"calc\")", "there is no function 'exec'")]
    [InlineData("round()", "round takes 1 to 2 values, not 0")]
    [InlineData("if(1, 2)", "if takes 3 values, not 2")]
    [InlineData("a = 1", "unexpected '='")]
    [InlineData("a; b", "unexpected ';'")]
    [InlineData("a[0]", "unexpected '['")]
    [InlineData("a ? b : c", "unexpected '?'")]
    [InlineData("fn => 1", "unexpected '='")]
    public void A_bad_formula_is_refused_with_a_reason_the_model_can_use(string formula, string expected) =>
        Assert.Contains(expected, Assert.Throws<ModException>(() => Formula.Parse(formula)).Message);

    [Fact]
    public void The_message_for_an_unknown_function_lists_the_real_ones()
    {
        var message = Assert.Throws<ModException>(() => Formula.Parse("eval(1)")).Message;
        Assert.Contains("round", message);
        Assert.Contains("coalesce", message);
    }

    [Fact]
    public void Formulas_are_bounded_in_length_depth_and_size()
    {
        Assert.Contains("at most 400 characters", Assert.Throws<ModException>(() => Formula.Parse(new string('1', 401))).Message);
        Assert.Contains("nested too deeply", Assert.Throws<ModException>(() => Formula.Parse(new string('-', 40) + "1")).Message);
        Assert.Contains("nested too deeply", Assert.Throws<ModException>(() => Formula.Parse(string.Concat(Enumerable.Repeat("(", 40)) + "1" + string.Concat(Enumerable.Repeat(")", 40)))).Message);
        var wide = string.Join(" + ", Enumerable.Repeat("1", 70));
        Assert.Contains("at most 120 parts", Assert.Throws<ModException>(() => Formula.Parse(wide)).Message);
    }

    [Fact]
    public void A_formula_that_runs_out_of_steps_stops_and_a_text_bomb_is_refused()
    {
        var sum = Formula.Parse(string.Join(" + ", Enumerable.Repeat("1", 30)));
        Assert.Equal("30", sum.Evaluate(_ => FormulaValue.Missing, new FormulaBudget(1000)).Display);
        Assert.Throws<FormulaLimitException>(() => sum.Evaluate(_ => FormulaValue.Missing, new FormulaBudget(10)));

        var big = new string('x', 700);
        var doubled = Formula.Parse("a + a");
        Assert.Throws<FormulaLimitException>(() => doubled.Evaluate(_ => FormulaValue.Of(big), new FormulaBudget(100)));
    }

    [Fact]
    public void Names_are_listed_once_each_in_order()
    {
        var f = Formula.Parse("if(system.battery.percent < 20, low, round(system.battery.percent) + settings.bonus + low)");
        Assert.Equal(["system.battery.percent", "low", "settings.bonus"], f.Names.ToArray());
    }

    // ---- inside a mod ----

    private const string Gauge = """
        {
          "id": "battery-nudge",
          "name": "Battery nudge",
          "kind": "widget",
          "level": "scripted",
          "placement": { "anchor": "top-right", "size": [220, 90] },
          "capabilities": ["system.battery"],
          "defs": {
            "pct": "round(system.battery.percent)",
            "low": "pct < settings.threshold",
            "mood": "if(low, \"Plug in soon\", \"Plenty left\")"
          },
          "settings": { "threshold": { "type": "number", "default": "20" } },
          "view": {
            "stack": [
              { "metric": "{pct}%", "label": "Battery" },
              { "text": "{mood}", "subtext": "{if(low, \"under \" + settings.threshold + \"%\", \"}\")}" }
            ]
          }
        }
        """;

    private sealed class Fixed(string prefix, params (string Path, string Value)[] values) : IDataProvider
    {
        public string Prefix => prefix;

        public bool TryGet(string path, out string value)
        {
            value = values.FirstOrDefault(v => v.Path == path).Value ?? "";
            return value.Length > 0;
        }
    }

    private static ViewBinder Binder(string percent, CapabilityBroker? broker = null)
    {
        broker ??= new CapabilityBroker();
        if (broker.GrantedTo("battery-nudge").Count == 0)
        {
            broker.Grant("battery-nudge", ["system.battery"]);
        }

        return new ViewBinder(broker, [new Fixed("system.battery", ("system.battery.percent", percent))]);
    }

    [Fact]
    public void A_scripted_widget_computes_with_defs_settings_and_data()
    {
        var mod = ModManifest.Parse(Gauge);
        var low = Binder("12.4").Bind(mod);
        Assert.Equal("12%", low.Children[0].Primary);
        Assert.Equal("Plug in soon", low.Children[1].Primary);
        Assert.Equal("under 20%", low.Children[1].Secondary);

        var fine = Binder("82").Bind(mod);
        Assert.Equal("82%", fine.Children[0].Primary);
        Assert.Equal("Plenty left", fine.Children[1].Primary);
        Assert.Equal("}", fine.Children[1].Secondary);

        var custom = Binder("40").Bind(mod, new Dictionary<string, string> { ["threshold"] = "50" });
        Assert.Equal("Plug in soon", custom.Children[1].Primary);
        Assert.Equal("under 50%", custom.Children[1].Secondary);
    }

    [Fact]
    public void A_scripted_widget_with_no_battery_reading_shows_dashes()
    {
        var mod = ModManifest.Parse(Gauge);
        var none = new ViewBinder(Granted(), []).Bind(mod);
        Assert.Equal("—%", none.Children[0].Primary);
        Assert.Equal("Plenty left", none.Children[1].Primary);
    }

    private static CapabilityBroker Granted()
    {
        var broker = new CapabilityBroker();
        broker.Grant("battery-nudge", ["system.battery"]);
        return broker;
    }

    [Fact]
    public void A_formula_cannot_read_what_was_not_granted_even_if_it_hides_the_read_in_a_def()
    {
        var sneaky = Gauge.Replace("\"pct\": \"round(system.battery.percent)\"", "\"pct\": \"round(system.battery.percent) + len(files.recent.first)\"");
        var mod = ModManifest.Parse(sneaky);

        var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
        var refused = Assert.Throws<ModException>(() => store.Review(sneaky));
        Assert.Contains("files.recent.first", refused.Message);

        // Even if it got past review (say the manifest was edited afterwards), every read is checked again.
        Assert.Throws<CapabilityDeniedException>(() => Binder("50").Bind(mod));
    }

    [Fact]
    public void The_approval_shows_the_formulas_in_full_and_what_they_read()
    {
        var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
        var proposal = store.Review(Gauge, Binder("30"));

        Assert.Equal(["system.battery.percent"], proposal.DataPaths.ToArray());
        Assert.Contains("low = pct < settings.threshold", proposal.Formulas);
        Assert.DoesNotContain("shows pct", proposal.Formulas);
        Assert.Contains("shows if(low, \"under \" + settings.threshold + \"%\", \"}\")", proposal.Formulas);
        Assert.Equal("30%", proposal.Preview!.Children[0].Primary);

        var card = ApprovalModel.FromMod(proposal);
        var calculates = Assert.Single(card.Rows, r => r.Badge == "Calculates");
        Assert.Equal(proposal.Formulas, calculates.Notes);
        Assert.Contains("no loops, no files, no network", card.RiskLine);
        Assert.Equal(Risk.Low, card.Risk);
        Assert.Contains(card.Rows, r => r.Badge == "Can read" && r.Text.Contains("battery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Editing_a_formula_after_approval_sends_the_mod_back_for_review()
    {
        var broker = new CapabilityBroker();
        var store = new ModStore(Path.Combine(_ws.Base, "mods"), broker);
        var proposal = store.Review(Gauge);
        var dir = Path.Combine(store.Root, "battery-nudge");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "mod.json"), Gauge);
        store.RecordApproval(proposal.Manifest, Gauge);
        Assert.Equal(ModStatus.Active, Assert.Single(store.LoadAll()).Status);

        File.WriteAllText(Path.Combine(dir, "mod.json"), Gauge.Replace("pct < settings.threshold", "pct < 99"));
        var again = Assert.Single(store.LoadAll());
        Assert.Equal(ModStatus.NeedsReview, again.Status);
        Assert.Empty(broker.GrantedTo("battery-nudge"));
    }

    [Theory]
    [InlineData("\"defs\": { \"x\": \"1\" }, \"level\": \"declarative\"", "add \"level\": \"scripted\"")]
    [InlineData("\"defs\": { \"a\": \"b\", \"b\": \"a\" }", "in a circle")]
    [InlineData("\"defs\": { \"a\": \"a + 1\" }", "in a circle")]
    [InlineData("\"defs\": { \"Bad\": \"1\" }", "must start with a lowercase letter")]
    [InlineData("\"defs\": { \"a\": \"1 +\" }", "ends where a value was expected")]
    [InlineData("\"defs\": { \"a\": 5 }", "must be a formula written as text")]
    [InlineData("\"defs\": { \"a\": \"nope\" }, \"capabilities\": []", "not a def")]
    [InlineData("\"defs\": { \"a\": \"settings.ghost\" }", "no setting called 'ghost'")]
    public void A_scripted_manifest_with_a_bad_def_is_refused(string tweak, string expected)
    {
        var json = """{"id":"x","name":"X","kind":"widget","level":"scripted","placement":{"size":[100,100]},"view":{"text":"hi"},__TWEAK__}""".Replace("__TWEAK__", tweak);
        if (tweak.Contains("\"level\": \"declarative\""))
        {
            json = json.Replace("\"level\":\"scripted\",", "");
        }

        Assert.Contains(expected, Assert.Throws<ModException>(() => ModManifest.Parse(json)).Message);
    }

    [Fact]
    public void A_declarative_view_that_tries_a_formula_is_told_how_to_ask_for_one()
    {
        var json = """{"id":"x","name":"X","kind":"widget","placement":{"size":[100,100]},"capabilities":["system.battery"],"view":{"text":"{round(system.battery.percent)}"}}""";
        var message = Assert.Throws<ModException>(() => ModManifest.Parse(json)).Message;
        Assert.Contains("is a formula", message);
        Assert.Contains("\"level\": \"scripted\"", message);
    }

    [Fact]
    public void Settings_placeholders_still_work_in_a_declarative_view()
    {
        var json = """{"id":"x","name":"X","kind":"widget","placement":{"size":[100,100]},"settings":{"name":{"type":"text","default":"you"}},"view":{"text":"hi {settings.name}"}}""";
        var bound = new ViewBinder(new CapabilityBroker(), []).Bind(ModManifest.Parse(json));
        Assert.Equal("hi you", bound.Primary);
    }

    [Fact]
    public void A_braced_string_inside_a_formula_does_not_end_the_placeholder()
    {
        var json = """{"id":"x","name":"X","kind":"widget","level":"scripted","placement":{"size":[100,100]},"view":{"text":"a {\"{x}\" + 1} b {2 + 3}"}}""";
        var bound = new ViewBinder(new CapabilityBroker(), []).Bind(ModManifest.Parse(json));
        Assert.Equal("a {x}1 b 5", bound.Primary);
    }

    [Fact]
    public void A_mod_cannot_exceed_its_step_budget_by_spreading_work_over_many_elements()
    {
        // 58 gauges, each with a 119-part formula in value, label and max. Together far more than one draw may spend.
        var heavy = string.Join(" + ", Enumerable.Repeat("1", 60));
        var gauge = "{\"gauge\":\"{" + heavy + "}\",\"label\":\"{" + heavy + "}\",\"max\":\"{" + heavy + "}\"}";
        var json = """{"id":"x","name":"X","kind":"widget","level":"scripted","placement":{"size":[100,100]},"view":{"stack":[__KIDS__]}}"""
            .Replace("__KIDS__", string.Join(",", Enumerable.Repeat(gauge, 58)));
        var mod = ModManifest.Parse(json);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bound = new ViewBinder(new CapabilityBroker(), []).Bind(mod);
        sw.Stop();

        var shown = bound.Children.Select(c => c.Primary).ToList();
        Assert.Equal("60", shown[0]);
        Assert.Equal("—", shown[^1]);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"{sw.Elapsed.TotalMilliseconds} ms");
    }

    private static string Generate(Random rng, int depth)
    {
        string[] names = ["x", "y.z", "settings.s", "system.battery.percent"];
        string[] unary1 = ["floor", "ceil", "abs", "len", "upper", "lower", "trim", "text", "number"];
        string[] binary2 = ["left", "right", "pad", "contains", "startsWith", "endsWith", "round"];
        string[] ops = ["+", "-", "*", "/", "%", "<", "<=", ">", ">=", "==", "!=", "&&", "||"];
        if (depth <= 0)
        {
            return rng.Next(4) switch
            {
                0 => rng.Next(-3, 120).ToString(System.Globalization.CultureInfo.InvariantCulture),
                1 => "\"" + new string((char)('a' + rng.Next(26)), rng.Next(0, 4)) + "\"",
                2 => rng.Next(2) == 0 ? "true" : "false",
                _ => names[rng.Next(names.Length)],
            };
        }

        return rng.Next(7) switch
        {
            0 => $"({Generate(rng, depth - 1)} {ops[rng.Next(ops.Length)]} {Generate(rng, depth - 1)})",
            1 => $"{unary1[rng.Next(unary1.Length)]}({Generate(rng, depth - 1)})",
            2 => $"{binary2[rng.Next(binary2.Length)]}({Generate(rng, depth - 1)}, {Generate(rng, depth - 1)})",
            3 => $"if({Generate(rng, depth - 1)}, {Generate(rng, depth - 1)}, {Generate(rng, depth - 1)})",
            4 => $"coalesce({Generate(rng, depth - 1)}, {Generate(rng, depth - 1)})",
            5 => $"clamp({Generate(rng, depth - 1)}, {Generate(rng, depth - 1)}, {Generate(rng, depth - 1)})",
            _ => $"!{Generate(rng, depth - 1)}",
        };
    }

    [Fact]
    public void Random_formulas_and_their_mutations_only_ever_fail_in_the_documented_ways()
    {
        // Whatever text arrives (a model's mistake, or an attacker's attempt), parsing either yields a
        // formula that finishes inside its budget or a ModException. Nothing else may escape.
        var rng = new Random(20261010);
        string[] junk = ["(", ")", ",", "\"", "'", "\\", "{", "}", "\u0000", "é", "..", "=", ";", "[", "?", " ", "9999999999999999999999", "||", "!"];
        var parsed = 0;
        var finished = 0;
        for (var i = 0; i < 20_000; i++)
        {
            var source = Generate(rng, rng.Next(0, 5));
            switch (rng.Next(3))
            {
                case 1 when source.Length > 0:
                    var at = rng.Next(source.Length);
                    source = source.Remove(at, 1);
                    break;
                case 2:
                    source = source.Insert(rng.Next(source.Length + 1), junk[rng.Next(junk.Length)]);
                    break;
            }

            Formula f;
            try
            {
                f = Formula.Parse(source);
            }
            catch (ModException)
            {
                continue;
            }

            parsed++;
            try
            {
                f.Evaluate(n => rng.Next(4) switch { 0 => FormulaValue.Missing, 1 => FormulaValue.Of(rng.Next(-5, 100)), 2 => FormulaValue.Of("t" + n), _ => FormulaValue.Of(true) }, new FormulaBudget(500));
                finished++;
            }
            catch (FormulaLimitException)
            {
            }
        }

        Assert.True(parsed > 10_000, $"only {parsed} of 20000 samples parsed; the generator is not exercising the evaluator");
        Assert.True(finished > 9_000, $"only {finished} finished");
    }

    [Fact]
    public void Formulas_are_deterministic()
    {
        var f = Formula.Parse("if(a > 3, upper(\"hi\") + text(a * 2), \"lo\")");
        for (var a = 0; a < 10; a++)
        {
            var first = f.Evaluate(_ => FormulaValue.Of(a), new FormulaBudget(100)).Display;
            Assert.Equal(first, f.Evaluate(_ => FormulaValue.Of(a), new FormulaBudget(100)).Display);
        }
    }

    [Fact]
    public void The_example_mods_in_the_repository_review_cleanly_and_draw()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "examples", "mods")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var files = Directory.GetFiles(Path.Combine(dir!.FullName, "examples", "mods"), "mod.json", SearchOption.AllDirectories);
        Assert.True(files.Length >= 2);

        var data = new Fixed("system.battery", ("system.battery.percent", "82"), ("system.battery.charging", "false"));
        var calendar = new Fixed("calendar.next", ("calendar.next.title", "Design review with the whole platform team"), ("calendar.next.startsIn", "in 25 min"));
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var id = ModManifest.Parse(json).Id;
            var broker = new CapabilityBroker();
            broker.Grant(id, ModManifest.Parse(json).Capabilities);
            var store = new ModStore(Path.Combine(_ws.Base, "mods"), new CapabilityBroker());
            var proposal = store.Review(json, new ViewBinder(broker, [new ClockProvider(), data, calendar]));
            Assert.NotNull(proposal.Preview);
            Assert.DoesNotContain("—", proposal.Preview!.Children.Select(c => c.Primary));
            Assert.Equal(id, Path.GetFileName(Path.GetDirectoryName(file)));
        }
    }

    [Fact]
    public void The_prompt_that_writes_mods_teaches_every_function_there_is_and_no_others()
    {
        var prompt = ClaudeOS.Core.Planning.ArtifactMaker.ModPrompt;
        foreach (var name in Formula.KnownFunctions.Split(", "))
        {
            Assert.Contains(name, prompt);
        }

        var listed = System.Text.RegularExpressions.Regex.Match(prompt, "only these functions: ([^.]*)\\.").Groups[1].Value.Split(", ");
        Assert.Equal(Formula.KnownFunctions.Split(", "), listed.Order(StringComparer.Ordinal));
    }
}
