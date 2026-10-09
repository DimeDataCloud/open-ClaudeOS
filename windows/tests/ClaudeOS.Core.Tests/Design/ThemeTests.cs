using System.Collections.Immutable;
using ClaudeOS.Core.Design;

namespace ClaudeOS.Core.Tests.Design;

public sealed class ThemeTests
{
    [Theory]
    [InlineData(Appearance.Light)]
    [InlineData(Appearance.Dark)]
    public void The_shipped_theme_is_legible_in_both_appearances(Appearance appearance)
    {
        var theme = ThemeResolver.Resolve(appearance);
        Assert.True(theme.IsValid, string.Join("; ", theme.Errors));
        Assert.Equal(8, theme.Series.Length);
    }

    [Theory]
    [InlineData("#EB6834")]
    [InlineData("#2A78D6")]
    [InlineData("#1BAF7A")]
    [InlineData("#E87BA4")]
    [InlineData("#4A3AA7")]
    [InlineData("#FFD400")] // a hard one: bright yellow
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    public void Any_accent_becomes_a_legible_theme(string accent)
    {
        foreach (var appearance in new[] { Appearance.Light, Appearance.Dark })
        {
            var theme = ThemeResolver.Resolve(appearance, new ThemeOverrides { Accent = accent });
            Assert.True(theme.IsValid, $"{accent} {appearance}: {string.Join("; ", theme.Errors)}");
            Assert.True(theme.Color("accent.action.ink").ContrastOn(theme.Color("accent.action")) >= 4.5);
        }
    }

    [Fact]
    public void Risk_and_diff_colours_cannot_be_themed_so_warnings_always_look_like_warnings()
    {
        var theme = ThemeResolver.Resolve(Appearance.Light, new ThemeOverrides
        {
            Colors = ImmutableDictionary<string, string>.Empty.Add("diff.remove", "#00FF00").Add("surface.scrim", "#FFFFFF00"),
        });
        Assert.False(theme.IsValid);
        Assert.Contains(theme.Errors, e => e.Contains("diff.remove") && e.Contains("fixed"));
        Assert.Contains(theme.Errors, e => e.Contains("surface.scrim"));
        Assert.Equal("#D03B3B1F", theme.Hex("diff.remove")); // untouched
    }

    [Fact]
    public void A_theme_that_makes_text_unreadable_is_refused_with_a_reason()
    {
        var theme = ThemeResolver.Resolve(Appearance.Light, new ThemeOverrides
        {
            Colors = ImmutableDictionary<string, string>.Empty.Add("ink.secondary", "#D8D4CC"),
        });
        Assert.False(theme.IsValid);
        var error = Assert.Single(theme.Errors, e => e.Contains("ink.secondary on surface.base"));
        Assert.Contains("needs 4.5:1", error);
    }

    [Fact]
    public void Out_of_range_knobs_and_bad_colours_are_reported()
    {
        var theme = ThemeResolver.Resolve(Appearance.Dark, new ThemeOverrides { RadiusScale = 5, TypeScale = 0.5, Accent = "orange" });
        Assert.Equal(3, theme.Errors.Length);
    }

    [Fact]
    public void Density_and_radius_scale_the_tokens()
    {
        var compact = ThemeResolver.Resolve(Appearance.Light, new ThemeOverrides { Density = Density.Compact, RadiusScale = 0.5 });
        Assert.Equal(13, compact.Space(16));
        Assert.Equal(6, compact.Radius(12));
    }

    [Theory]
    [InlineData("#FAF9F7", 250, 249, 247, 255)]
    [InlineData("#FAF9F7B8", 250, 249, 247, 0xB8)]
    [InlineData("#fff", 255, 255, 255, 255)]
    public void Colours_parse(string hex, int r, int g, int b, int a)
    {
        var c = Rgba.Parse(hex);
        Assert.Equal(((byte)r, (byte)g, (byte)b, (byte)a), (c.R, c.G, c.B, c.A));
    }

    [Fact]
    public void Colour_maths_agree_with_known_values()
    {
        Assert.Equal(21.0, Rgba.Parse("#000000").ContrastOn(Rgba.Parse("#FFFFFF")), 2);
        Assert.Equal(16.53, Rgba.Parse("#1B1A18").ContrastOn(Rgba.Parse("#FAF9F7")), 2);
        var original = Rgba.Parse("#EB6834");
        var (l, c, h) = original.ToOklch();
        Assert.Equal(original, Rgba.FromOklch(l, c, h));
        Assert.Throws<FormatException>(() => Rgba.Parse("nope"));
    }

    [Fact]
    public void The_generated_tokens_match_the_design_source()
    {
        // design/tokens.json is the source of truth; Tokens.g.cs is generated from it. CI also runs
        // `node design/build-tokens.mjs --check`, but this keeps the guarantee inside `dotnet test`.
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "tokens.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var light = doc.RootElement.GetProperty("color").GetProperty("light");
        foreach (var prop in light.EnumerateObject())
        {
            Assert.Equal(prop.Value.GetString()!.ToUpperInvariant(), Tokens.Light[prop.Name]);
        }

        Assert.Equal(Tokens.ThemeName, doc.RootElement.GetProperty("name").GetString());
    }
}
