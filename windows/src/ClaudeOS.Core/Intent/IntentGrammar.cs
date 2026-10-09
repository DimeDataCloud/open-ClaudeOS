using System.Text.RegularExpressions;

namespace ClaudeOS.Core.Intent;

public sealed record GrammarResult(UserIntent Intent, bool Matched);

/// <summary>
/// The zero-token path: a small, deterministic English grammar for the most common requests.
/// Opening, finding, window commands and undo never reach a model. Anything it does not
/// recognise comes back as <see cref="UnclearIntent"/> so the router can ask the on-device
/// classifier, and only then the cloud.
/// </summary>
public static partial class IntentGrammar
{
    public static GrammarResult Parse(string text)
    {
        var raw = text.Trim();
        var s = Normalize(raw);
        if (s.Length == 0)
        {
            return new(new UnclearIntent(raw), false);
        }

        if (AcceptRx().IsMatch(s))
        {
            return new(new SuggestionReplyIntent(raw, true), true);
        }

        if (DeclineRx().IsMatch(s))
        {
            return new(new SuggestionReplyIntent(raw, false), true);
        }

        if (UndoRx().IsMatch(s))
        {
            return new(new UndoIntent(raw), true);
        }

        if (PutBackRx().IsMatch(s))
        {
            return new(new WindowIntent(raw, WindowCommand.PutBack), true);
        }

        if (TryWindow(s, out var command))
        {
            return new(new WindowIntent(raw, command), true);
        }

        if (TryMake(s, raw, out var make))
        {
            return new(make, true);
        }

        if (ChangeRx().IsMatch(s))
        {
            return new(new ChangeIntent(raw, s), true);
        }

        if (FindRx().Match(s) is { Success: true } find)
        {
            return new(new FindIntent(raw, Clean(find.Groups["q"].Value)), true);
        }

        if (OpenRx().Match(s) is { Success: true } open)
        {
            var app = open.Groups["app"].Success ? Clean(open.Groups["app"].Value) : null;
            return new(new OpenIntent(raw, Clean(open.Groups["q"].Value), app), true);
        }

        return new(new UnclearIntent(raw), false);
    }

    private static string Normalize(string text)
    {
        var s = text.Trim().TrimEnd('.', '!', '?', ' ').ToLowerInvariant();
        s = PoliteRx().Replace(s, "");
        return WhitespaceRx().Replace(s, " ").Trim();
    }

    private static string Clean(string query)
    {
        var q = ArticleRx().Replace(query.Trim(), "");
        return q.Trim().Trim('"', '\'', '“', '”');
    }

    private static bool TryWindow(string s, out WindowCommand command)
    {
        command = default;
        if (SnapLeftRx().IsMatch(s)) { command = WindowCommand.SnapLeft; return true; }
        if (SnapRightRx().IsMatch(s)) { command = WindowCommand.SnapRight; return true; }
        if (MaximizeRx().IsMatch(s)) { command = WindowCommand.Maximize; return true; }
        if (MinimizeRx().IsMatch(s)) { command = WindowCommand.Minimize; return true; }
        if (CloseRx().IsMatch(s)) { command = WindowCommand.Close; return true; }
        if (CenterRx().IsMatch(s)) { command = WindowCommand.CenterOnScreen; return true; }
        if (PinRx().IsMatch(s)) { command = WindowCommand.Pin; return true; }
        return false;
    }

    private static bool TryMake(string s, string raw, out MakeIntent intent)
    {
        intent = null!;

        // "make the bars blue" edits what is on screen; "make me a ..." creates something.
        var editing = EditPhraseRx().IsMatch(s);
        var creating = CreateVerbRx().IsMatch(s) && !editing;
        MakeKind? kind = null;
        if (ModRx().IsMatch(s)) { kind = s.Contains("theme") ? MakeKind.Theme : s.Contains("widget") ? MakeKind.Widget : MakeKind.Mod; }
        else if (ChartRx().IsMatch(s)) { kind = MakeKind.Chart; }
        else if (DiagramRx().IsMatch(s)) { kind = MakeKind.Diagram; }
        else if (TableRx().IsMatch(s)) { kind = MakeKind.Table; }
        else if (ReportRx().IsMatch(s)) { kind = MakeKind.Report; }

        if (kind is null)
        {
            return creating && s.Length > 12 && MakeNounRx().IsMatch(s) && Set(out intent, raw, MakeKind.Other, s);
        }

        // Precision over recall: a chart noun alone ("open the sales chart") is something that
        // exists. Unsure phrasings go to the on-device classifier, then the cloud.
        return (creating || ExplicitFormRx().IsMatch(s) || ImplicitMakeRx().IsMatch(s)) && Set(out intent, raw, kind.Value, s);
    }

    private static bool Set(out MakeIntent intent, string raw, MakeKind kind, string request)
    {
        intent = new MakeIntent(raw, kind, request);
        return true;
    }

    [GeneratedRegex(@"^(?:(?:please|pls|hey claude|hey|ok|okay|claude|can you|could you|would you|will you|i want you to|i'd like you to|i would like to|i want to|i need to|let's|lets)[,:]?\s+)+|(?:\s+(?:please|for me|thanks|thank you))+$")]
    private static partial Regex PoliteRx();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRx();

    [GeneratedRegex(@"^(?:the|my|a|an|that|this)\s+")]
    private static partial Regex ArticleRx();

    [GeneratedRegex(@"^(?:yes|yeah|yep|yup|sure|ok|okay|do it|do that|go ahead|sounds good|that works|please do|make (?:it|that) (?:automatic|a rule)|always|yes,? (?:do (?:it|that)|always|make it automatic))$")]
    private static partial Regex AcceptRx();

    [GeneratedRegex(@"^(?:no|nope|nah|not now|no thanks|no,? thanks|never mind|nevermind|don'?t|not really|skip(?: it| that)?|dismiss)$")]
    private static partial Regex DeclineRx();

    [GeneratedRegex(@"^(?:undo|undo that|undo it|undo the last(?: change| thing)?|revert(?: that| the last change)?|take that back)$")]
    private static partial Regex UndoRx();

    [GeneratedRegex(@"^(?:put (?:it|them|everything|things) back|restore (?:the |my )?(?:layout|windows)|reset (?:the |my )?(?:layout|windows)|put the windows back)$")]
    private static partial Regex PutBackRx();

    [GeneratedRegex(@"^(?:snap|move|put|dock|send)(?: (?:this|that|it|the window|this window))?(?: to)?(?: the)? left(?: half| side)?$|^left half$")]
    private static partial Regex SnapLeftRx();

    [GeneratedRegex(@"^(?:snap|move|put|dock|send)(?: (?:this|that|it|the window|this window))?(?: to)?(?: the)? right(?: half| side)?$|^right half$")]
    private static partial Regex SnapRightRx();

    [GeneratedRegex(@"^(?:maximi[sz]e|full ?screen|go full ?screen|make (?:this|it) full ?screen)(?: this| it| the window| this window)?$")]
    private static partial Regex MaximizeRx();

    [GeneratedRegex(@"^(?:minimi[sz]e|hide)(?: this| it| the window| this window)?$")]
    private static partial Regex MinimizeRx();

    [GeneratedRegex(@"^(?:close|dismiss|get rid of)(?: this| it| that| the window| this window)?$")]
    private static partial Regex CloseRx();

    [GeneratedRegex(@"^(?:center|centre)(?: this| it| the window| this window)?(?: on (?:the )?screen)?$")]
    private static partial Regex CenterRx();

    [GeneratedRegex(@"^(?:pin|keep)(?: this| it| that)?(?: on top| above)?$|^(?:always )?on top$")]
    private static partial Regex PinRx();

    [GeneratedRegex(@"\b(?:widget|mod|theme|plugin)\b")]
    private static partial Regex ModRx();

    [GeneratedRegex(@"\b(?:graph|chart|plot|histogram|visuali[sz]e|bar chart|pie chart|line chart|sparkline)\b")]
    private static partial Regex ChartRx();

    [GeneratedRegex(@"\b(?:diagram|flowchart|flow chart|mind ?map|org chart|sequence diagram|architecture)\b")]
    private static partial Regex DiagramRx();

    [GeneratedRegex(@"\b(?:table|spreadsheet of|pivot)\b")]
    private static partial Regex TableRx();

    [GeneratedRegex(@"\b(?:report|summary|summari[sz]e|write ?up|one[- ]pager|memo|brief|overview)\b")]
    private static partial Regex ReportRx();

    [GeneratedRegex(@"^(?:make|create|build|generate|draw|write|draft|compose|produce|put together|whip up|give)\b")]
    private static partial Regex CreateVerbRx();

    [GeneratedRegex(@"\b(?:something|page|doc|document|sheet|list|note|plan|timeline|calendar|dashboard|tracker)\b")]
    private static partial Regex MakeNounRx();

    [GeneratedRegex(@"^(?:make|turn|change|set) (?:the|it|this|that|its|these|those)\b")]
    private static partial Regex EditPhraseRx();

    [GeneratedRegex(@"\b(?:as|into) an? (?:\w+ ){0,2}(?:graph|chart|plot|table|diagram|report|summary|visuali[sz]ation)\b|\ban? (?:\w+ ){0,2}(?:graph|chart|plot|table|diagram|report|summary|one[- ]pager|memo) (?:of|on|for|about|showing|with|from)\b")]
    private static partial Regex ExplicitFormRx();

    [GeneratedRegex(@"^(?:graph|chart|plot|visuali[sz]e|diagram|summari[sz]e|tabulate|map out|sketch)\b")]
    private static partial Regex ImplicitMakeRx();

    [GeneratedRegex(@"^(?:make|change|set|turn|switch|add|remove|hide|sort|rename|resize|swap|use|colou?r|recolou?r|bold|increase|decrease|shrink|enlarge)\b.+$")]
    private static partial Regex ChangeRx();

    [GeneratedRegex(@"^(?:find|search(?: for)?|look for|locate|where(?:'s| is| are)|where did i (?:put|save|leave)|which file has)\s+(?<q>.+)$")]
    private static partial Regex FindRx();

    [GeneratedRegex(@"^(?:open|launch|start|show(?: me)?|pull up|bring up|go to|display|view|read)\s+(?<q>.+?)(?:\s+(?:in|with|using|on)\s+(?<app>[a-z0-9 .+#_-]{2,30}))?$")]
    private static partial Regex OpenRx();
}
