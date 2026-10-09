using System.Collections.Immutable;

namespace ClaudeOS.Core.Design;

public enum Appearance { Light, Dark }

public enum Density { Compact, Comfortable, Spacious }

public enum MotionPreference
{
    /// <summary>Springs and presence animation.</summary>
    Full,

    /// <summary>Crossfades only; the presence stays still but changes colour and label.</summary>
    Reduced,
}

public enum Backdrop { Mica, Acrylic, Solid }

/// <summary>
/// What a theme mod (or the settings screen) may change. Everything else is fixed. In
/// particular the risk and diff colours on the approval card are never overridable, so no
/// mod can restyle a warning into something that looks calm.
/// </summary>
public sealed record ThemeOverrides
{
    /// <summary>Any colour; the resolver derives an accessible action colour, glow and mark.</summary>
    public string? Accent { get; init; }

    /// <summary>Scales every corner radius, 0.0 (square) to 2.0.</summary>
    public double RadiusScale { get; init; } = 1;

    public Density Density { get; init; } = Density.Comfortable;

    public MotionPreference Motion { get; init; } = MotionPreference.Full;

    public Backdrop Backdrop { get; init; } = Backdrop.Mica;

    /// <summary>Scales text, 0.85 to 1.4.</summary>
    public double TypeScale { get; init; } = 1;

    public string? FontUi { get; init; }

    /// <summary>Direct colour overrides for the roles in <see cref="ThemeResolver.Overridable"/>.</summary>
    public ImmutableDictionary<string, string> Colors { get; init; } = ImmutableDictionary<string, string>.Empty;
}

public sealed record ResolvedTheme(
    Appearance Appearance,
    ImmutableDictionary<string, string> Colors,
    ImmutableArray<string> Series,
    double RadiusScale,
    Density Density,
    MotionPreference Motion,
    Backdrop Backdrop,
    double TypeScale,
    string FontUi,
    ImmutableArray<string> Errors)
{
    public bool IsValid => Errors.IsEmpty;

    public Rgba Color(string role) => Rgba.Parse(Colors[role]);

    public string Hex(string role) => Colors[role];

    public double Space(int dips) => Math.Round(dips * Density switch { Density.Compact => 0.8, Density.Spacious => 1.25, _ => 1.0 });

    public double Radius(int dips) => Math.Round(dips * RadiusScale);
}

/// <summary>
/// Turns the default tokens plus a mod's overrides into a theme every surface can read, or a
/// list of plain-language reasons it was refused. Legibility is enforced here, once, so a
/// theme mod cannot make text unreadable or hide a risk indicator.
/// </summary>
public static class ThemeResolver
{
    /// <summary>The roles a theme may change. Status, diff and scrim colours are deliberately absent.</summary>
    public static readonly ImmutableHashSet<string> Overridable =
    [
        "surface.base", "surface.raised", "surface.sunken", "surface.glass",
        "ink.primary", "ink.secondary", "ink.tertiary", "line.hairline", "line.strong",
        "accent.mark", "accent.glow", "accent.action", "accent.action.ink", "accent.soft", "focus.ring",
    ];

    private static readonly (string Fg, string Bg, double Min, string Why)[] Requirements =
    [
        ("ink.primary", "surface.base", 7, "body text"),
        ("ink.primary", "surface.raised", 4.5, "text on cards"),
        ("ink.secondary", "surface.base", 4.5, "secondary text"),
        ("ink.secondary", "surface.raised", 4.5, "secondary text on cards"),
        ("ink.tertiary", "surface.base", 4.5, "hint text"),
        ("accent.action.ink", "accent.action", 4.5, "button labels"),
        ("accent.mark", "surface.base", 3, "accent marks"),
        ("focus.ring", "surface.base", 3, "the keyboard focus ring"),
    ];

    public static ResolvedTheme Resolve(Appearance appearance, ThemeOverrides? overrides = null)
    {
        overrides ??= new ThemeOverrides();
        var errors = ImmutableArray.CreateBuilder<string>();
        var colors = (appearance == Appearance.Light ? Tokens.Light : Tokens.Dark).ToDictionary(kv => kv.Key, kv => kv.Value);
        var series = (appearance == Appearance.Light ? Tokens.SeriesLight : Tokens.SeriesDark).ToImmutableArray();

        if (overrides.Accent is not null)
        {
            if (Rgba.TryParse(overrides.Accent, out var accent))
            {
                foreach (var (role, value) in DeriveAccent(accent, appearance))
                {
                    colors[role] = value;
                }
            }
            else
            {
                errors.Add($"accent '{overrides.Accent}' is not a colour");
            }
        }

        foreach (var (role, value) in overrides.Colors)
        {
            if (!Overridable.Contains(role))
            {
                errors.Add($"'{role}' cannot be changed by a theme" + (role.StartsWith("diff", StringComparison.Ordinal) || role.StartsWith("risk", StringComparison.Ordinal) ? " (risk and diff colours are fixed so a warning always looks like one)" : ""));
            }
            else if (!Rgba.TryParse(value, out _))
            {
                errors.Add($"'{role}' value '{value}' is not a colour");
            }
            else
            {
                colors[role] = Rgba.Parse(value).ToHex();
            }
        }

        if (overrides.RadiusScale is < 0 or > 2)
        {
            errors.Add("radius scale must be between 0 and 2");
        }

        if (overrides.TypeScale is < 0.85 or > 1.4)
        {
            errors.Add("type scale must be between 0.85 and 1.4");
        }

        foreach (var (fg, bg, min, why) in Requirements)
        {
            var ratio = Rgba.Parse(colors[fg]).ContrastOn(Rgba.Parse(colors[bg]).Over(new Rgba(255, 255, 255)));
            if (ratio < min)
            {
                errors.Add($"{why} would be hard to read: {fg} on {bg} has contrast {ratio:0.0}:1, needs {min:0.#}:1");
            }
        }

        return new ResolvedTheme(
            appearance,
            colors.ToImmutableDictionary(),
            series,
            Math.Clamp(overrides.RadiusScale, 0, 2),
            overrides.Density,
            overrides.Motion,
            overrides.Backdrop,
            Math.Clamp(overrides.TypeScale, 0.85, 1.4),
            overrides.FontUi ?? Tokens.Font.Ui,
            errors.ToImmutable());
    }

    /// <summary>
    /// Any hue becomes a usable accent: the mark keeps the person's colour (nudged into the
    /// band that reads on the surface), and the button colour is darkened or lightened until its
    /// label reaches 4.5:1.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> DeriveAccent(Rgba accent, Appearance appearance)
    {
        var (_, chroma, hue) = accent.ToOklch();
        chroma = Math.Clamp(chroma, 0.10, 0.20);
        var light = appearance == Appearance.Light;

        var surface = Rgba.Parse((light ? Tokens.Light : Tokens.Dark)["surface.base"]);
        var mark = Fit(light ? 0.66 : 0.62, light ? -0.01 : 0.01, chroma, hue, c => c.ContrastOn(surface) >= 3.05);
        var glow = Rgba.FromOklch(light ? 0.78 : 0.74, chroma * 0.9, hue);
        var ink = light ? new Rgba(255, 255, 255) : new Rgba(0x1C, 0x1B, 0x1A);

        var action = Fit(light ? 0.55 : 0.68, light ? -0.01 : 0.01, chroma, hue, c => ink.ContrastOn(c) >= 4.6);

        yield return new("accent.mark", mark.ToHex());
        yield return new("accent.glow", glow.ToHex());
        yield return new("accent.action", action.ToHex());
        yield return new("accent.action.ink", ink.ToHex());
        yield return new("accent.soft", (mark with { A = light ? (byte)0x14 : (byte)0x2E }).ToHex());
    }

    /// <summary>Walks lightness from a starting point until the colour passes <paramref name="ok"/>.</summary>
    private static Rgba Fit(double l, double step, double chroma, double hue, Func<Rgba, bool> ok)
    {
        var color = Rgba.FromOklch(l, chroma, hue);
        for (var i = 0; i < 90 && !ok(color); i++)
        {
            l = Math.Clamp(l + step, 0.02, 0.99);
            color = Rgba.FromOklch(l, chroma, hue);
        }

        return color;
    }
}
