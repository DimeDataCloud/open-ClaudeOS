using System.Text.Json;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests.Safety;

/// <summary>
/// The protected-name check rests on a small <c>fnmatch</c>. Python's is the reference, so 6,000 random
/// name and pattern pairs (generated once by Python, in fixtures/glob-cases.json) must agree.
/// </summary>
public sealed class GlobDifferentialTests
{
    private sealed record Case(string Name, string Pattern, bool Match);

    private static List<Case> Load()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "glob-cases.json")));
        return [.. doc.RootElement.GetProperty("cases").EnumerateArray().Select(c => new Case(c.GetProperty("name").GetString()!, c.GetProperty("pattern").GetString()!, c.GetProperty("match").GetBoolean()))];
    }

    [Fact]
    public void Our_glob_agrees_with_pythons_fnmatch_on_random_cases()
    {
        var cases = Load();
        Assert.True(cases.Count >= 6000);
        var disagreements = cases.Where(c => Glob.Match(c.Name, c.Pattern) != c.Match)
            .Select(c => $"'{c.Name}' ~ '{c.Pattern}' should be {c.Match}")
            .ToList();
        Assert.True(disagreements.Count == 0, $"{disagreements.Count} of {cases.Count} differ, for example: {string.Join(" | ", disagreements.Take(12))}");
    }
}
