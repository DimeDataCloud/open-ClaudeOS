using System.Diagnostics;

namespace ClaudeOS.Core.Intent;

public enum RouteSource { Grammar, Npu, Cloud }

public sealed record Routing(UserIntent Intent, RouteSource Source, TimeSpan Elapsed);

public enum IntentLabel { Open, Find, Make, Change, Window, Other }

public sealed record ClassifierResult(IntentLabel Label, double Confidence);

/// <summary>
/// A small on-device model that decides between "open", "find", "make" and "change" for phrasings
/// the grammar does not know. <see cref="LocalIntentClassifier"/> does this on the CPU today; a
/// Windows ML model on the NPU can replace it behind this interface. It routes; it never approves anything.
/// </summary>
public interface IIntentClassifier
{
    Task<ClassifierResult> ClassifyAsync(string text, CancellationToken ct = default);
}

/// <summary>Last resort: ask Claude to classify. Costs tokens, so the router tries everything
/// else first. Returns <see cref="UnclearIntent"/> if even the model cannot tell.</summary>
public interface ICloudIntentResolver
{
    Task<UserIntent> ResolveAsync(string text, CancellationToken ct = default);
}

/// <summary>Grammar first (no model), then the on-device classifier, then the cloud.</summary>
public sealed class IntentRouter(IIntentClassifier? classifier = null, ICloudIntentResolver? cloud = null, double npuThreshold = 0.8)
{
    public async Task<Routing> RouteAsync(string text, CancellationToken ct = default)
    {
        var clock = Stopwatch.StartNew();
        var grammar = IntentGrammar.Parse(text);
        if (grammar.Matched)
        {
            return new Routing(grammar.Intent, RouteSource.Grammar, clock.Elapsed);
        }

        if (classifier is not null)
        {
            var result = await classifier.ClassifyAsync(text, ct).ConfigureAwait(false);
            if (result.Confidence >= npuThreshold && FromLabel(result.Label, text) is { } intent)
            {
                return new Routing(intent, RouteSource.Npu, clock.Elapsed);
            }
        }

        if (cloud is not null)
        {
            var resolved = await cloud.ResolveAsync(text, ct).ConfigureAwait(false);
            return new Routing(resolved, RouteSource.Cloud, clock.Elapsed);
        }

        return new Routing(new UnclearIntent(text), RouteSource.Grammar, clock.Elapsed);
    }

    private static UserIntent? FromLabel(IntentLabel label, string text) => label switch
    {
        IntentLabel.Open => IntentGrammar.QueryOf(text) is { Length: > 0 } open ? new OpenIntent(text, open) : null,
        IntentLabel.Find => IntentGrammar.QueryOf(text) is { Length: > 0 } find ? new FindIntent(text, find) : null,
        IntentLabel.Make => new MakeIntent(text, IntentGrammar.MakeKindOf(text), text.Trim()),
        IntentLabel.Change => new ChangeIntent(text, text.Trim()),
        _ => null,
    };
}
