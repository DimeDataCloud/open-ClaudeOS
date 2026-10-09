namespace ClaudeOS.Core.Intent;

/// <summary>
/// The on-device step between the grammar and the cloud: a naive Bayes classifier over words and
/// word pairs, trained at start-up on about two hundred phrases, that decides what kind of request an
/// unfamiliar phrasing is. It runs on the CPU in well under a millisecond on any machine, with no
/// model file and no network. It sits behind <see cref="IIntentClassifier"/>, so a Windows ML model on
/// the NPU can replace it without touching the router. It only routes; it never approves anything.
/// It is built to abstain: when it has not seen the words, or two labels are close, its confidence is
/// low and the router carries on to the cloud.
/// </summary>
public sealed class LocalIntentClassifier : IIntentClassifier
{
    // Chosen on the development phrases in the tests: how fast evidence is tempered as a phrase gets longer.
    private const double TemperatureExponent = 0.3;

    private static readonly IntentLabel[] Labels = Enum.GetValues<IntentLabel>();

    private readonly Dictionary<string, double[]> _logLikelihood = new(StringComparer.Ordinal);
    private readonly double[] _logPrior = new double[Labels.Length];

    public LocalIntentClassifier()
        : this(IntentTraining.Examples)
    {
    }

    public LocalIntentClassifier(IEnumerable<(IntentLabel Label, string Phrase)> examples)
    {
        var counts = new Dictionary<string, int[]>(StringComparer.Ordinal);
        var perLabelFeatures = new int[Labels.Length];
        var perLabelDocs = new int[Labels.Length];
        var docs = 0;
        foreach (var (label, phrase) in examples)
        {
            var c = (int)label;
            perLabelDocs[c]++;
            docs++;
            foreach (var feature in Features(phrase).Distinct(StringComparer.Ordinal))
            {
                if (!counts.TryGetValue(feature, out var row))
                {
                    counts[feature] = row = new int[Labels.Length];
                }

                row[c]++;
                perLabelFeatures[c]++;
            }
        }

        const double alpha = 0.4;
        var vocabulary = counts.Count;
        for (var c = 0; c < Labels.Length; c++)
        {
            _logPrior[c] = Math.Log((perLabelDocs[c] + 1.0) / (docs + Labels.Length));
        }

        foreach (var (feature, row) in counts)
        {
            var logs = new double[Labels.Length];
            for (var c = 0; c < Labels.Length; c++)
            {
                logs[c] = Math.Log((row[c] + alpha) / (perLabelFeatures[c] + (alpha * vocabulary)));
            }

            _logLikelihood[feature] = logs;
        }
    }

    public Task<ClassifierResult> ClassifyAsync(string text, CancellationToken ct = default) => Task.FromResult(Classify(text));

    public ClassifierResult Classify(string text)
    {
        var tokens = Tokens(text);
        var known = 0;
        var score = (double[])_logPrior.Clone();
        foreach (var feature in Features(text).Distinct(StringComparer.Ordinal))
        {
            if (!_logLikelihood.TryGetValue(feature, out var logs))
            {
                continue;
            }

            known++;
            for (var c = 0; c < score.Length; c++)
            {
                score[c] += logs[c];
            }
        }

        var knownWords = tokens.Count(t => _logLikelihood.ContainsKey(t));
        if (known == 0 || knownWords == 0)
        {
            return new ClassifierResult(IntentLabel.Other, 0);
        }

        // Longer phrases pile up evidence and would otherwise look certain; temper by length.
        var temperature = Math.Max(1.0, Math.Pow(known, TemperatureExponent));
        var max = score.Max();
        var weights = score.Select(s => Math.Exp((s - max) / temperature)).ToArray();
        var total = weights.Sum();
        var best = Array.IndexOf(weights, weights.Max());
        var confidence = weights[best] / total;

        // If most of the words are ones it has never seen, it is guessing from the rest.
        var coverage = (double)knownWords / tokens.Count;
        if (coverage < 0.6)
        {
            confidence *= coverage / 0.6;
        }

        return new ClassifierResult(Labels[best], Math.Round(confidence, 4));
    }

    /// <summary>The lower-cased words of a phrase, lightly stemmed, with punctuation dropped.</summary>
    internal static List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        var word = new System.Text.StringBuilder();
        void Flush()
        {
            if (word.Length > 0)
            {
                tokens.Add(Stem(word.ToString()));
                word.Clear();
            }
        }

        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch == '\'')
            {
                word.Append(ch);
            }
            else
            {
                Flush();
            }
        }

        Flush();
        return tokens;
    }

    private static IEnumerable<string> Features(string text)
    {
        var tokens = Tokens(text);
        foreach (var t in tokens)
        {
            yield return t;
        }

        for (var i = 0; i + 1 < tokens.Count; i++)
        {
            yield return tokens[i] + " " + tokens[i + 1];
        }
    }

    private static string Stem(string word)
    {
        word = word.Replace("'", "", StringComparison.Ordinal);
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal))
        {
            return word[..^3] + "y";
        }

        if (word.Length > 4 && word.EndsWith("ing", StringComparison.Ordinal))
        {
            return word[..^3];
        }

        if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal))
        {
            return word[..^1];
        }

        return word;
    }
}
