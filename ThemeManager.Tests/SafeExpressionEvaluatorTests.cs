using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class SafeExpressionEvaluatorTests
{
    [Fact]
    public void Evaluator_HandlesArithmeticAndMeasureReferences()
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cpu"] = 80,
            ["Ram"] = 40
        };

        var ok = SafeExpressionEvaluator.TryEvaluate(
            "clamp((Cpu + Ram) / 2, 0, 100)",
            name => values.TryGetValue(name, out var value) ? value : null,
            out var result);

        Assert.True(ok);
        Assert.Equal(60, result, precision: 8);
    }

    [Fact]
    public void Evaluator_RejectsUnsupportedCode()
    {
        var ok = SafeExpressionEvaluator.TryEvaluate(
            "System.IO.File.Delete('x')",
            _ => 1,
            out _);

        Assert.False(ok);
    }

    [Fact]
    public void Evaluator_RejectsDivideByZeroAndUnknownNames()
    {
        Assert.False(SafeExpressionEvaluator.TryEvaluate("10 / 0", _ => null, out _));
        Assert.False(SafeExpressionEvaluator.TryEvaluate("Missing + 1", _ => null, out _));
    }
}
