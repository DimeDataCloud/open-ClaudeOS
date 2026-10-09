using System.Collections.Immutable;
using System.Globalization;

namespace ClaudeOS.Core.Mods;

/// <summary>
/// A value inside a formula: a number, some text, a yes/no, or <em>missing</em> (a reading that was
/// not available). Missing flows through arithmetic and string functions instead of throwing, so a
/// widget whose data is briefly absent shows a dash rather than an error.
/// </summary>
public readonly struct FormulaValue
{
    private enum Kind : byte { Missing, Number, Text, Flag }

    private readonly Kind _kind;
    private readonly double _number;
    private readonly string? _text;

    private FormulaValue(Kind kind, double number, string? text)
    {
        _kind = kind;
        _number = number;
        _text = text;
    }

    public static FormulaValue Missing => default;

    public static FormulaValue Of(double d) => double.IsFinite(d) ? new(Kind.Number, d, null) : Missing;

    public static FormulaValue Of(string s) => new(Kind.Text, 0, s);

    public static FormulaValue Of(bool b) => new(Kind.Flag, b ? 1 : 0, null);

    public bool IsMissing => _kind == Kind.Missing;

    public bool IsText => _kind == Kind.Text;

    /// <summary>What a data provider handed over: numbers and yes/no become typed, anything else is text.</summary>
    public static FormulaValue FromData(string raw)
    {
        if (raw.Length == 0 || raw == "—")
        {
            return Missing;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d))
        {
            return Of(d);
        }

        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return Of(true);
        }

        return raw.Equals("false", StringComparison.OrdinalIgnoreCase) ? Of(false) : Of(raw);
    }

    public bool TryNumber(out double value)
    {
        switch (_kind)
        {
            case Kind.Number:
                value = _number;
                return true;
            case Kind.Text when double.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d):
                value = d;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    public bool Truthy => _kind switch
    {
        Kind.Number or Kind.Flag => _number != 0,
        Kind.Text => _text!.Length > 0,
        _ => false,
    };

    /// <summary>How the value reads on screen.</summary>
    public string Display => _kind switch
    {
        Kind.Number => _number == Math.Truncate(_number) && Math.Abs(_number) < 1e15
            ? ((long)_number).ToString(CultureInfo.InvariantCulture)
            : _number.ToString("G10", CultureInfo.InvariantCulture),
        Kind.Text => _text!,
        Kind.Flag => _number != 0 ? "true" : "false",
        _ => "—",
    };
}

/// <summary>How much a formula may do. Spent per step; running out renders a dash, never a hang.</summary>
public sealed class FormulaBudget(int steps)
{
    public const int PerWidget = 20_000;

    public int Remaining { get; private set; } = steps;

    internal void Spend()
    {
        if (--Remaining < 0)
        {
            throw new FormulaLimitException("a formula used up its step budget");
        }
    }
}

/// <summary>A formula that did more than the limits allow while running.</summary>
public sealed class FormulaLimitException(string message) : Exception(message);

internal abstract class FormulaNode;

internal sealed class Lit(FormulaValue value) : FormulaNode
{
    public FormulaValue Value { get; } = value;
}

internal sealed class NameRef(string path) : FormulaNode
{
    public string Path { get; } = path;
}

internal sealed class Unary(char op, FormulaNode operand) : FormulaNode
{
    public char Op { get; } = op;

    public FormulaNode Operand { get; } = operand;
}

internal sealed class Binary(string op, FormulaNode left, FormulaNode right) : FormulaNode
{
    public string Op { get; } = op;

    public FormulaNode Left { get; } = left;

    public FormulaNode Right { get; } = right;
}

internal sealed class Call(string function, ImmutableArray<FormulaNode> args) : FormulaNode
{
    public string Function { get; } = function;

    public ImmutableArray<FormulaNode> Args { get; } = args;
}

/// <summary>
/// The language scripted mods are written in: a small, total expression language. It has numbers,
/// text, yes/no, the usual operators, a short list of functions and names that point at data the
/// mod was granted. It has no loops, no recursion, no assignment, no way to call anything outside
/// this file, so every formula finishes, and the only thing it can ever do is compute a value
/// from what the person approved. That is the whole sandbox: there is nothing to escape into.
/// </summary>
public sealed class Formula
{
    public const int MaxSource = 400;
    public const int MaxNodes = 120;
    public const int MaxDepth = 16;
    public const int MaxText = 1024;

    private static readonly Dictionary<string, (int Min, int Max)> Functions = new(StringComparer.Ordinal)
    {
        ["if"] = (3, 3), ["round"] = (1, 2), ["floor"] = (1, 1), ["ceil"] = (1, 1), ["abs"] = (1, 1),
        ["min"] = (2, 8), ["max"] = (2, 8), ["clamp"] = (3, 3), ["number"] = (1, 1), ["text"] = (1, 1),
        ["len"] = (1, 1), ["upper"] = (1, 1), ["lower"] = (1, 1), ["trim"] = (1, 1), ["left"] = (2, 2), ["right"] = (2, 2),
        ["pad"] = (2, 2), ["contains"] = (2, 2), ["startsWith"] = (2, 2), ["endsWith"] = (2, 2), ["coalesce"] = (2, 8),
    };

    private readonly FormulaNode _root;

    private Formula(string source, FormulaNode root, ImmutableArray<string> names)
    {
        Source = source;
        _root = root;
        Names = names;
    }

    public string Source { get; }

    /// <summary>Every name the formula reads, once each, in the order it first appears. A name with
    /// no dot refers to another formula (a def); one with a dot is a path into granted data or a setting.</summary>
    public ImmutableArray<string> Names { get; }

    public static string KnownFunctions => string.Join(", ", Functions.Keys.Order(StringComparer.Ordinal));

    public static Formula Parse(string source)
    {
        if (source.Length > MaxSource)
        {
            throw new ModException($"a formula may be at most {MaxSource} characters");
        }

        var parser = new Parser(source);
        var root = parser.ParseAll();
        return new Formula(source, root, [.. parser.Names]);
    }

    /// <summary>Run the formula. <paramref name="resolve"/> answers names; the broker lives behind it.</summary>
    public FormulaValue Evaluate(Func<string, FormulaValue> resolve, FormulaBudget budget) => new Evaluator(resolve, budget).Eval(_root);

    private sealed class Parser(string source)
    {
        private int _i;
        private int _nodes;
        private int _depth;

        public List<string> Names { get; } = [];

        public FormulaNode ParseAll()
        {
            var node = Or();
            Skip();
            if (_i < source.Length)
            {
                throw Fail($"unexpected '{source[_i]}'");
            }

            return node;
        }

        private ModException Fail(string message) => new($"formula \"{source}\": {message} (at character {_i + 1})");

        private T Count<T>(T node) where T : FormulaNode
        {
            if (++_nodes > MaxNodes)
            {
                throw Fail($"too long; a formula may have at most {MaxNodes} parts");
            }

            return node;
        }

        private void Skip()
        {
            while (_i < source.Length && char.IsWhiteSpace(source[_i]))
            {
                _i++;
            }
        }

        private bool Take(string op)
        {
            Skip();
            if (string.CompareOrdinal(source, _i, op, 0, op.Length) != 0)
            {
                return false;
            }

            _i += op.Length;
            return true;
        }

        private FormulaNode Or()
        {
            var left = And();
            while (Take("||"))
            {
                left = Count(new Binary("||", left, And()));
            }

            return left;
        }

        private FormulaNode And()
        {
            var left = Equality();
            while (Take("&&"))
            {
                left = Count(new Binary("&&", left, Equality()));
            }

            return left;
        }

        private FormulaNode Equality()
        {
            var left = Relation();
            while (true)
            {
                var op = Take("==") ? "==" : Take("!=") ? "!=" : null;
                if (op is null)
                {
                    return left;
                }

                left = Count(new Binary(op, left, Relation()));
            }
        }

        private FormulaNode Relation()
        {
            var left = Sum();
            while (true)
            {
                var op = Take("<=") ? "<=" : Take(">=") ? ">=" : Take("<") ? "<" : Take(">") ? ">" : null;
                if (op is null)
                {
                    return left;
                }

                left = Count(new Binary(op, left, Sum()));
            }
        }

        private FormulaNode Sum()
        {
            var left = Product();
            while (true)
            {
                var op = Take("+") ? "+" : Take("-") ? "-" : null;
                if (op is null)
                {
                    return left;
                }

                left = Count(new Binary(op, left, Product()));
            }
        }

        private FormulaNode Product()
        {
            var left = Prefix();
            while (true)
            {
                var op = Take("*") ? "*" : Take("/") ? "/" : Take("%") ? "%" : null;
                if (op is null)
                {
                    return left;
                }

                left = Count(new Binary(op, left, Prefix()));
            }
        }

        private FormulaNode Prefix()
        {
            Skip();
            if (++_depth > MaxDepth)
            {
                throw Fail($"nested too deeply; at most {MaxDepth} levels");
            }

            try
            {
                if (Take("!"))
                {
                    return Count(new Unary('!', Prefix()));
                }

                if (Take("-"))
                {
                    return Count(new Unary('-', Prefix()));
                }

                return Primary();
            }
            finally
            {
                _depth--;
            }
        }

        private FormulaNode Primary()
        {
            Skip();
            if (_i >= source.Length)
            {
                throw Fail("the formula ends where a value was expected");
            }

            var c = source[_i];
            if (c == '(')
            {
                _i++;
                var inner = Or();
                if (!Take(")"))
                {
                    throw Fail("missing ')'");
                }

                return inner;
            }

            if (char.IsAsciiDigit(c))
            {
                return Count(new Lit(FormulaValue.Of(Number())));
            }

            if (c is '"' or '\'')
            {
                return Count(new Lit(FormulaValue.Of(Quoted(c))));
            }

            if (char.IsAsciiLetter(c) || c == '_')
            {
                return NameOrCall();
            }

            throw Fail($"unexpected '{c}'");
        }

        private double Number()
        {
            var start = _i;
            while (_i < source.Length && char.IsAsciiDigit(source[_i]))
            {
                _i++;
            }

            if (_i + 1 < source.Length && source[_i] == '.' && char.IsAsciiDigit(source[_i + 1]))
            {
                _i++;
                while (_i < source.Length && char.IsAsciiDigit(source[_i]))
                {
                    _i++;
                }
            }

            if (_i < source.Length && (char.IsAsciiLetter(source[_i]) || source[_i] == '_'))
            {
                throw Fail("a number cannot run straight into letters");
            }

            return double.Parse(source.AsSpan(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private string Quoted(char quote)
        {
            _i++;
            var sb = new System.Text.StringBuilder();
            while (true)
            {
                if (_i >= source.Length)
                {
                    throw Fail("text is missing its closing quote");
                }

                var c = source[_i++];
                if (c == quote)
                {
                    return sb.ToString();
                }

                if (c != '\\')
                {
                    sb.Append(c);
                }
                else
                {
                    if (_i >= source.Length)
                    {
                        throw Fail("text is missing its closing quote");
                    }

                    var escaped = source[_i++];
                    sb.Append(escaped switch
                    {
                        'n' => '\n',
                        '\\' or '"' or '\'' => escaped,
                        _ => throw Fail($"'\\{escaped}' is not an escape; use \\n, \\\\, \\\" or \\'"),
                    });
                }

                if (sb.Length > MaxText)
                {
                    throw Fail($"text may be at most {MaxText} characters");
                }
            }
        }

        private FormulaNode NameOrCall()
        {
            var start = _i;
            while (_i < source.Length && (char.IsAsciiLetterOrDigit(source[_i]) || source[_i] is '_' or '.'))
            {
                _i++;
            }

            var name = source[start.._i];
            if (name.EndsWith('.') || name.Contains("..", StringComparison.Ordinal))
            {
                throw Fail($"'{name}' is not a valid name");
            }

            if (name == "true" || name == "false")
            {
                return Count(new Lit(FormulaValue.Of(name == "true")));
            }

            Skip();
            if (_i < source.Length && source[_i] == '(')
            {
                if (name.Contains('.'))
                {
                    throw Fail($"'{name}' is a path, not a function");
                }

                if (!Functions.TryGetValue(name, out var arity))
                {
                    throw Fail($"there is no function '{name}'; the functions are {KnownFunctions}");
                }

                _i++;
                var args = ImmutableArray.CreateBuilder<FormulaNode>();
                if (!Take(")"))
                {
                    do
                    {
                        args.Add(Or());
                    }
                    while (Take(","));

                    if (!Take(")"))
                    {
                        throw Fail("missing ')'");
                    }
                }

                if (args.Count < arity.Min || args.Count > arity.Max)
                {
                    throw Fail(arity.Min == arity.Max
                        ? $"{name} takes {arity.Min} values, not {args.Count}"
                        : $"{name} takes {arity.Min} to {arity.Max} values, not {args.Count}");
                }

                return Count(new Call(name, args.ToImmutable()));
            }

            if (!Names.Contains(name))
            {
                Names.Add(name);
            }

            return Count(new NameRef(name));
        }
    }

    private sealed class Evaluator(Func<string, FormulaValue> resolve, FormulaBudget budget)
    {
        public FormulaValue Eval(FormulaNode node)
        {
            budget.Spend();
            return node switch
            {
                Lit l => l.Value,
                NameRef n => resolve(n.Path),
                Unary u => Prefix(u),
                Binary b => Infix(b),
                Call c => Invoke(c),
                _ => FormulaValue.Missing,
            };
        }

        private FormulaValue Prefix(Unary u)
        {
            var v = Eval(u.Operand);
            if (u.Op == '!')
            {
                return FormulaValue.Of(!v.Truthy);
            }

            return v.TryNumber(out var d) ? FormulaValue.Of(-d) : FormulaValue.Missing;
        }

        private FormulaValue Infix(Binary b)
        {
            if (b.Op == "&&")
            {
                return FormulaValue.Of(Eval(b.Left).Truthy && Eval(b.Right).Truthy);
            }

            if (b.Op == "||")
            {
                return FormulaValue.Of(Eval(b.Left).Truthy || Eval(b.Right).Truthy);
            }

            var l = Eval(b.Left);
            var r = Eval(b.Right);
            switch (b.Op)
            {
                case "==":
                    return FormulaValue.Of(Same(l, r));
                case "!=":
                    return FormulaValue.Of(!Same(l, r));
                case "<" or "<=" or ">" or ">=":
                    if (l.IsMissing || r.IsMissing)
                    {
                        return FormulaValue.Of(false);
                    }

                    var order = l.TryNumber(out var x) && r.TryNumber(out var y) ? x.CompareTo(y) : string.CompareOrdinal(l.Display, r.Display);
                    return FormulaValue.Of(b.Op switch { "<" => order < 0, "<=" => order <= 0, ">" => order > 0, _ => order >= 0 });
                case "+" when l.IsText || r.IsText:
                    return l.IsMissing || r.IsMissing ? FormulaValue.Missing : Bounded(l.Display + r.Display);
            }

            if (!l.TryNumber(out var a) || !r.TryNumber(out var c))
            {
                return FormulaValue.Missing;
            }

            return FormulaValue.Of(b.Op switch
            {
                "+" => a + c,
                "-" => a - c,
                "*" => a * c,
                "/" => c == 0 ? double.NaN : a / c,
                _ => c == 0 ? double.NaN : a % c,
            });
        }

        private static bool Same(FormulaValue l, FormulaValue r)
        {
            if (l.IsMissing || r.IsMissing)
            {
                return l.IsMissing && r.IsMissing;
            }

            return l.TryNumber(out var a) && r.TryNumber(out var b) ? a == b : string.Equals(l.Display, r.Display, StringComparison.Ordinal);
        }

        private static FormulaValue Bounded(string text) =>
            text.Length > MaxText ? throw new FormulaLimitException("a formula built text that is too long") : FormulaValue.Of(text);

        private FormulaValue Invoke(Call c)
        {
            if (c.Function == "if")
            {
                return Eval(c.Args[0]).Truthy ? Eval(c.Args[1]) : Eval(c.Args[2]);
            }

            var args = new FormulaValue[c.Args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                args[i] = Eval(c.Args[i]);
            }

            switch (c.Function)
            {
                case "coalesce":
                    foreach (var a in args)
                    {
                        if (!a.IsMissing && a.Display.Length > 0)
                        {
                            return a;
                        }
                    }

                    return FormulaValue.Missing;
                case "contains" or "startsWith" or "endsWith":
                    if (args[0].IsMissing || args[1].IsMissing)
                    {
                        return FormulaValue.Of(false);
                    }

                    var (s, t) = (args[0].Display, args[1].Display);
                    return FormulaValue.Of(c.Function switch
                    {
                        "contains" => s.Contains(t, StringComparison.OrdinalIgnoreCase),
                        "startsWith" => s.StartsWith(t, StringComparison.OrdinalIgnoreCase),
                        _ => s.EndsWith(t, StringComparison.OrdinalIgnoreCase),
                    });
            }

            if (args.Any(a => a.IsMissing))
            {
                return FormulaValue.Missing;
            }

            var nums = new double[args.Length];
            switch (c.Function)
            {
                case "round" or "floor" or "ceil" or "abs" or "min" or "max" or "clamp" or "number":
                    for (var i = 0; i < args.Length; i++)
                    {
                        if (!args[i].TryNumber(out nums[i]))
                        {
                            return FormulaValue.Missing;
                        }
                    }

                    break;
            }

            switch (c.Function)
            {
                case "round":
                    return FormulaValue.Of(Math.Round(nums[0], args.Length > 1 ? (int)Math.Clamp(nums[1], 0, 10) : 0, MidpointRounding.AwayFromZero));
                case "floor":
                    return FormulaValue.Of(Math.Floor(nums[0]));
                case "ceil":
                    return FormulaValue.Of(Math.Ceiling(nums[0]));
                case "abs":
                    return FormulaValue.Of(Math.Abs(nums[0]));
                case "min":
                    return FormulaValue.Of(nums.Min());
                case "max":
                    return FormulaValue.Of(nums.Max());
                case "clamp":
                    return FormulaValue.Of(nums[1] <= nums[2] ? Math.Clamp(nums[0], nums[1], nums[2]) : double.NaN);
                case "number":
                    return FormulaValue.Of(nums[0]);
                case "text":
                    return FormulaValue.Of(args[0].Display);
                case "len":
                    return FormulaValue.Of(args[0].Display.Length);
                case "upper":
                    return FormulaValue.Of(args[0].Display.ToUpperInvariant());
                case "lower":
                    return FormulaValue.Of(args[0].Display.ToLowerInvariant());
                case "trim":
                    return FormulaValue.Of(args[0].Display.Trim());
                case "left" or "right":
                    if (!args[1].TryNumber(out var n))
                    {
                        return FormulaValue.Missing;
                    }

                    var text = args[0].Display;
                    var take = (int)Math.Clamp(n, 0, text.Length);
                    return FormulaValue.Of(c.Function == "left" ? text[..take] : text[^take..]);
                case "pad":
                    return args[1].TryNumber(out var width)
                        ? FormulaValue.Of(args[0].Display.PadLeft((int)Math.Clamp(width, 0, 32), '0'))
                        : FormulaValue.Missing;
                default:
                    return FormulaValue.Missing;
            }
        }
    }
}
