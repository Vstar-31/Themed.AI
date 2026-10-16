using System.Globalization;

namespace ThemeManager.Core.Skins;

/// <summary>
/// Small arithmetic-only expression evaluator for Formula measures. It intentionally supports a
/// fixed grammar and a tiny whitelist of functions; it never invokes reflection, dynamic code,
/// scripting engines or user-provided methods.
/// </summary>
public static class SafeExpressionEvaluator
{
    public static bool TryEvaluate(
        string? expression,
        Func<string, double?> resolveIdentifier,
        out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(expression)) return false;

        try
        {
            var parser = new Parser(expression, resolveIdentifier);
            value = parser.ParseExpression();
            parser.ExpectEnd();
            return double.IsFinite(value);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (DivideByZeroException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly Func<string, double?> _resolve;
        private int _index;

        public Parser(string text, Func<string, double?> resolve)
        {
            _text = text;
            _resolve = resolve;
        }

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('+')) value += ParseTerm();
                else if (TryConsume('-')) value -= ParseTerm();
                else return value;
            }
        }

        private double ParseTerm()
        {
            var value = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('*')) value *= ParseUnary();
                else if (TryConsume('/'))
                {
                    var divisor = ParseUnary();
                    if (Math.Abs(divisor) < double.Epsilon) throw new DivideByZeroException();
                    value /= divisor;
                }
                else return value;
            }
        }

        private double ParseUnary()
        {
            SkipWhitespace();
            if (TryConsume('+')) return ParseUnary();
            if (TryConsume('-')) return -ParseUnary();
            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            SkipWhitespace();

            if (TryConsume('('))
            {
                var nested = ParseExpression();
                Expect(')');
                return nested;
            }

            if (_index < _text.Length && (char.IsDigit(_text[_index]) || _text[_index] == '.'))
                return ParseNumber();

            var identifier = ParseIdentifier();
            SkipWhitespace();

            if (TryConsume('('))
            {
                var args = new List<double>();
                SkipWhitespace();
                if (!TryConsume(')'))
                {
                    do
                    {
                        args.Add(ParseExpression());
                        SkipWhitespace();
                    }
                    while (TryConsume(','));

                    Expect(')');
                }

                return ApplyFunction(identifier, args);
            }

            var resolved = _resolve(identifier);
            if (resolved is null)
                throw new FormatException($"Unknown identifier '{identifier}'.");
            return resolved.Value;
        }

        private double ParseNumber()
        {
            var start = _index;
            bool sawExponent = false;
            while (_index < _text.Length)
            {
                var ch = _text[_index];
                if (char.IsDigit(ch) || ch == '.')
                {
                    _index++;
                    continue;
                }

                if ((ch == 'e' || ch == 'E') && !sawExponent)
                {
                    sawExponent = true;
                    _index++;
                    if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-'))
                        _index++;
                    continue;
                }

                break;
            }

            var token = _text[start.._index];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"Invalid number '{token}'.");
            return value;
        }

        private string ParseIdentifier()
        {
            SkipWhitespace();
            var start = _index;
            while (_index < _text.Length &&
                   (char.IsLetterOrDigit(_text[_index]) || _text[_index] is '_' or '.' or '$'))
                _index++;

            if (start == _index)
                throw new FormatException("Expected a number, identifier or function.");
            return _text[start.._index];
        }

        private static double ApplyFunction(string name, IReadOnlyList<double> args)
        {
            switch (name.ToLowerInvariant())
            {
                case "abs" when args.Count == 1:
                    return Math.Abs(args[0]);
                case "min" when args.Count >= 1:
                    return args.Min();
                case "max" when args.Count >= 1:
                    return args.Max();
                case "round" when args.Count is 1 or 2:
                    return args.Count == 1 ? Math.Round(args[0]) : Math.Round(args[0], checked((int)args[1]));
                case "floor" when args.Count == 1:
                    return Math.Floor(args[0]);
                case "ceil" when args.Count == 1:
                    return Math.Ceiling(args[0]);
                case "clamp" when args.Count == 3:
                    return Math.Clamp(args[0], Math.Min(args[1], args[2]), Math.Max(args[1], args[2]));
                default:
                    throw new FormatException($"Unsupported function '{name}' or wrong argument count.");
            }
        }

        public void ExpectEnd()
        {
            SkipWhitespace();
            if (_index != _text.Length)
                throw new FormatException("Unexpected trailing expression content.");
        }

        private void Expect(char character)
        {
            SkipWhitespace();
            if (!TryConsume(character))
                throw new FormatException($"Expected '{character}'.");
        }

        private bool TryConsume(char character)
        {
            if (_index < _text.Length && _text[_index] == character)
            {
                _index++;
                return true;
            }
            return false;
        }

        private void SkipWhitespace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
                _index++;
        }
    }
}
