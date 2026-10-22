using ThemeManager.Core.Skins;

namespace ThemeManager.Tests;

public sealed class SafeExpressionEvaluatorTests
{
    [Theory]
    [InlineData("+", "")]
    [InlineData("-", "")]
    [InlineData("(", ")")]
    [InlineData("abs(", ")")]
    public void Evaluator_RejectsExcessiveNesting(string prefix, string suffix)
    {
        var expression = string.Concat(Enumerable.Repeat(prefix, 64)) + "1"
            + string.Concat(Enumerable.Repeat(suffix, 64));

        Assert.False(SafeExpressionEvaluator.TryEvaluate(expression, _ => null, out _));
    }

    [Theory]
    [InlineData("+", "")]
    [InlineData("-", "")]
    [InlineData("(", ")")]
    [InlineData("abs(", ")")]
    public void Evaluator_AcceptsNestingWithinBudget(string prefix, string suffix)
    {
        var expression = string.Concat(Enumerable.Repeat(prefix, 63)) + "1"
            + string.Concat(Enumerable.Repeat(suffix, 63));

        Assert.True(SafeExpressionEvaluator.TryEvaluate(expression, _ => null, out var value));
        Assert.Equal(prefix == "-" ? -1d : 1d, value);
    }

    [Fact]
    public void Evaluator_UsesOneBudgetForMixedNesting()
    {
        var expression = string.Concat(Enumerable.Repeat("-abs((", 22)) + "1"
            + string.Concat(Enumerable.Repeat("))", 22));

        Assert.False(SafeExpressionEvaluator.TryEvaluate(expression, _ => null, out _));
    }

    [Fact]
    public void Evaluator_EnforcesLengthBeforeResolvingIdentifiers()
    {
        var resolved = false;
        var expression = "Cpu" + new string(' ', 4094);

        Assert.False(SafeExpressionEvaluator.TryEvaluate(expression, _ => { resolved = true; return 1; }, out _));
        Assert.False(resolved);
        Assert.True(SafeExpressionEvaluator.TryEvaluate("1" + new string(' ', 4095), _ => null, out var value));
        Assert.Equal(1d, value);
        Assert.False(SafeExpressionEvaluator.TryEvaluate(new string('-', 131072) + "1", _ => null, out _));
    }

    [Fact]
    public void Evaluator_ReleasesDepthBudgetBetweenSiblingExpressions()
    {
        var expression = string.Join("+", Enumerable.Repeat("abs(-1)", 400));

        Assert.True(SafeExpressionEvaluator.TryEvaluate(expression, _ => null, out var value));
        Assert.Equal(400d, value);
        Assert.True(SafeExpressionEvaluator.TryEvaluate("max(1e2, round(3.14159, 2), -(-2))", _ => null, out value));
        Assert.Equal(100d, value);
    }

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
