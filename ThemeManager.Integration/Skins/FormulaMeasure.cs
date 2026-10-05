using Microsoft.Extensions.Logging;
using ThemeManager.Core.Skins;

namespace ThemeManager.Integration.Skins;

/// <summary>A numeric measure backed by the safe Core expression grammar.</summary>
public sealed class FormulaMeasure : IMeasure
{
    private readonly ILogger? _logger;
    private readonly string _expression;
    private readonly Func<string, double?> _resolve;
    private double _value;
    private string _text = "—";

    public string Name { get; }
    public double Value => _value;
    public string Text => _text;

    public FormulaMeasure(string name, string? expression, Func<string, double?> resolve, ILogger? logger = null)
    {
        Name = name;
        _expression = expression ?? "";
        _resolve = resolve;
        _logger = logger;
    }

    public void Refresh()
    {
        if (SafeExpressionEvaluator.TryEvaluate(_expression, _resolve, out var value))
        {
            _value = value;
            _text = double.IsFinite(value)
                ? value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                : "—";
            return;
        }

        _value = 0;
        _text = "—";
        _logger?.LogDebug("Formula measure \"{Name}\" could not evaluate expression \"{Expression}\"", Name, _expression);
    }
}
