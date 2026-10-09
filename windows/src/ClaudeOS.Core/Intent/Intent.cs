namespace ClaudeOS.Core.Intent;

/// <summary>
/// What the person asked for, decided without a model wherever possible. The first question
/// is always the same: do they want to <em>see</em> something that exists, <em>make</em>
/// something new, or <em>change</em> something already on screen?
/// </summary>
public abstract record UserIntent(string Raw);

/// <summary>Show something that already exists. Changes nothing, so it needs no approval.</summary>
public sealed record OpenIntent(string Raw, string Query, string? App = null) : UserIntent(Raw);

public sealed record FindIntent(string Raw, string Query) : UserIntent(Raw);

public enum WindowCommand { SnapLeft, SnapRight, Maximize, Minimize, Close, CenterOnScreen, Pin, PutBack }

/// <summary>Move or resize windows. Reversible by "put it back", so it needs no approval.</summary>
public sealed record WindowIntent(string Raw, WindowCommand Command) : UserIntent(Raw);

public sealed record UndoIntent(string Raw) : UserIntent(Raw);

public enum MakeKind { Chart, Table, Report, Diagram, Widget, Mod, Theme, Other }

/// <summary>Create something new. Produces new files only, so it applies straight away with Undo.</summary>
public sealed record MakeIntent(string Raw, MakeKind Kind, string Request) : UserIntent(Raw);

/// <summary>Edit the thing on screen: "make the bars blue".</summary>
public sealed record ChangeIntent(string Raw, string Request) : UserIntent(Raw);

/// <summary>Needs a model to understand; the router sends it on.</summary>
/// <summary>"Yes" or "not now" to something the presence offered, such as a placement rule.</summary>
public sealed record SuggestionReplyIntent(string Raw, bool Accepted) : UserIntent(Raw);

public sealed record UnclearIntent(string Raw) : UserIntent(Raw);
