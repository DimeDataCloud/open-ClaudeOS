using System.Globalization;
using System.Text.Json.Nodes;

namespace ClaudeOS.Core.Planning;

/// <summary>US dollars per million tokens.</summary>
public sealed record ModelPrice(double Input, double Output, double CacheRead, double CacheWrite)
{
    public double Cost(TokenUsage u) => ((u.Input * Input) + (u.Output * Output) + (u.CacheRead * CacheRead) + (u.CacheWrite * CacheWrite)) / 1_000_000;
}

public sealed class BudgetExceededException(string message) : Exception(message);

public sealed record Budget(double? DailyUsd = null, double? MonthlyUsd = null);

/// <summary>
/// Every model request is logged locally with its tokens and estimated cost, shown on demand,
/// and checked against daily and monthly caps before the next request is made. "Measure it":
/// the cheapest token is the one never sent, and you can only see that if you count.
/// </summary>
public sealed class TokenLedger(string path, Budget? budget = null, TimeProvider? clock = null)
{
    private static readonly Dictionary<string, ModelPrice> Prices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-opus-5-5"] = new(4, 20, 0.20, 5),
        ["claude-opus-5"] = new(5, 25, 0.25, 6.25),
        ["claude-sonnet-5-5"] = new(2, 10, 0.20, 2.5),
        ["claude-sonnet-5"] = new(2, 10, 0.20, 2.5),
        ["claude-haiku-5-5"] = new(0.10, 0.50, 0.01, 0.125),
        ["claude-fable-5-1"] = new(10, 50, 0.25, 12.5),
    };

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly object _gate = new();

    public static ModelPrice PriceOf(string model) => Prices.TryGetValue(model, out var p) ? p : Prices["claude-opus-5-5"];

    /// <summary>Throws if a cap has been reached. Call before every request.</summary>
    public void EnsureWithinBudget()
    {
        if (budget is null)
        {
            return;
        }

        var now = _clock.GetUtcNow();
        if (budget.DailyUsd is { } daily && SpentSince(now.Date) >= daily)
        {
            throw new BudgetExceededException($"daily model budget of ${daily:0.00} reached; it resets at midnight UTC");
        }

        if (budget.MonthlyUsd is { } monthly && SpentSince(new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)) >= monthly)
        {
            throw new BudgetExceededException($"monthly model budget of ${monthly:0.00} reached");
        }
    }

    public void Record(string purpose, string model, TokenUsage usage)
    {
        var line = new JsonObject
        {
            ["ts"] = _clock.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["purpose"] = purpose,
            ["model"] = model,
            ["input"] = usage.Input,
            ["output"] = usage.Output,
            ["cache_read"] = usage.CacheRead,
            ["cache_write"] = usage.CacheWrite,
            ["usd"] = Math.Round(PriceOf(model).Cost(usage), 6),
        };
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, line.ToJsonString() + "\n");
        }
    }

    public IReadOnlyList<(DateTimeOffset At, string Purpose, string Model, TokenUsage Usage, double Usd)> Entries()
    {
        if (!File.Exists(path))
        {
            return [];
        }

        lock (_gate)
        {
            return [.. File.ReadAllLines(path).Where(l => l.Length > 0).Select(l =>
            {
                var o = JsonNode.Parse(l)!.AsObject();
                return (DateTimeOffset.Parse(o["ts"]!.GetValue<string>(), CultureInfo.InvariantCulture),
                    o["purpose"]!.GetValue<string>(),
                    o["model"]!.GetValue<string>(),
                    new TokenUsage(o["input"]!.GetValue<long>(), o["output"]!.GetValue<long>(), o["cache_read"]!.GetValue<long>(), o["cache_write"]!.GetValue<long>()),
                    o["usd"]!.GetValue<double>());
            })];
        }
    }

    public double SpentSince(DateTimeOffset since) => Entries().Where(e => e.At >= since).Sum(e => e.Usd);

    public TokenUsage TotalSince(DateTimeOffset since) => Entries().Where(e => e.At >= since).Aggregate(TokenUsage.None, (a, e) => a + e.Usage);

    /// <summary>One line the shell can show on demand: what today cost.</summary>
    public string Summary()
    {
        var today = new DateTimeOffset(_clock.GetUtcNow().Date, TimeSpan.Zero);
        var usage = TotalSince(today);
        return $"today: {usage.Total:N0} tokens, about ${SpentSince(today):0.00}";
    }
}
