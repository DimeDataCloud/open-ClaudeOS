using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace ClaudeOS.Core.Mods;

/// <summary>
/// Everything about the scripted level that is decided before a mod is ever shown: which formulas a
/// manifest holds, whether they are well formed, what they read, and what the person is shown.
/// A formula that does not parse, names something that does not exist, or reads in a circle makes the
/// manifest invalid, with a message the model can use to repair it.
/// </summary>
internal static partial class Script
{
    public const int MaxDefs = 24;

    [GeneratedRegex("^[a-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex DefNameRx();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex PathRx();

    public static bool IsScripted(ModManifest m) => m.Level == ModLevel.Scripted;

    /// <summary>The formula text inside every <c>{...}</c> of the view.</summary>
    public static IEnumerable<string> ViewBodies(ModManifest m) =>
        m.View is null ? [] : m.View.Texts().SelectMany(t => Placeholders.Spans(t, quoteAware: true).Select(s => s.Body)).Where(b => b.Length > 0);

    public static void Validate(ModManifest m)
    {
        if (!IsScripted(m))
        {
            if (m.Defs.Count > 0)
            {
                throw new ModException("defs are formulas; add \"level\": \"scripted\" to use them");
            }

            foreach (var body in ViewBodies(m))
            {
                if (!PathRx().IsMatch(body))
                {
                    throw new ModException($"{{{body}}} is a formula; a declarative view only takes paths like {{system.battery.percent}}. Add \"level\": \"scripted\" to use formulas");
                }
            }

            return;
        }

        if (m.Defs.Count > MaxDefs)
        {
            throw new ModException($"a mod may have at most {MaxDefs} defs");
        }

        var settingKeys = m.Settings.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        var compiled = new Dictionary<string, Formula>(StringComparer.Ordinal);
        foreach (var (name, source) in m.Defs)
        {
            if (!DefNameRx().IsMatch(name))
            {
                throw new ModException($"def name '{name}' must start with a lowercase letter and use only letters and digits");
            }

            compiled[name] = Formula.Parse(source);
        }

        var all = compiled.Values.Concat(ViewBodies(m).Select(Formula.Parse));
        foreach (var formula in all)
        {
            foreach (var name in formula.Names)
            {
                if (name.Contains('.'))
                {
                    if (name.StartsWith("settings.", StringComparison.Ordinal) && !settingKeys.Contains(name["settings.".Length..]))
                    {
                        throw new ModException($"formula \"{formula.Source}\" reads {name} but the mod has no setting called '{name["settings.".Length..]}'");
                    }
                }
                else if (!compiled.ContainsKey(name))
                {
                    throw new ModException($"formula \"{formula.Source}\" uses '{name}', which is not a def. Data is read by path, like system.battery.percent");
                }
            }
        }

        foreach (var name in compiled.Keys)
        {
            Cycle(name, compiled, []);
        }
    }

    private static void Cycle(string name, Dictionary<string, Formula> defs, List<string> path)
    {
        if (path.Contains(name))
        {
            throw new ModException($"defs refer to each other in a circle: {string.Join(" → ", path.Append(name))}");
        }

        path.Add(name);
        foreach (var next in defs[name].Names.Where(n => !n.Contains('.')))
        {
            Cycle(next, defs, path);
        }

        path.RemoveAt(path.Count - 1);
    }

    public static ImmutableArray<string> DataPaths(ModManifest m)
    {
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        if (IsScripted(m))
        {
            foreach (var f in m.Defs.Values.Concat(ViewBodies(m)).Select(Formula.Parse))
            {
                foreach (var name in f.Names.Where(n => n.Contains('.') && !n.StartsWith("settings.", StringComparison.Ordinal)))
                {
                    paths.Add(name);
                }
            }
        }
        else if (m.View is not null)
        {
            foreach (var p in m.View.Paths().Where(p => !p.StartsWith("settings.", StringComparison.Ordinal)))
            {
                paths.Add(p);
            }
        }

        return [.. paths];
    }

    /// <summary>The formulas as the approval card shows them: each def, then each expression the view displays.</summary>
    public static ImmutableArray<string> Describe(ModManifest m)
    {
        if (!IsScripted(m))
        {
            return [];
        }

        var lines = new List<string>();
        lines.AddRange(m.Defs.Select(d => $"{d.Key} = {d.Value}"));
        // A body that is just a path or just the name of a def says nothing the defs above do not.
        lines.AddRange(ViewBodies(m).Where(b => !PathRx().IsMatch(b) && !DefNameRx().IsMatch(b)).Distinct(StringComparer.Ordinal).Select(b => $"shows {b}"));
        return [.. lines];
    }
}
