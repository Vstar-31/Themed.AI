using System;
using System.Collections.Generic;
using System.Diagnostics;
using ThemeManager.Core.NLP;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Generating 100s of Prompts ===");

        var vibeAdjectives = new[] { "dark", "bright", "cozy", "cyberpunk", "warm", "cold", "minimalist", "neon", "gloomy" };
        var vibeNouns = new[] { "ocean", "forest", "city", "cafe", "sunset", "matrix", "synthwave", "space", "attic" };
        var vibeExtras = new[] { "with red accents", "lots of blue", "mostly black and white", "feeling nostalgic", "super chaotic" };

        var vibePrompts = new List<string>();
        foreach (var adj in vibeAdjectives)
        foreach (var noun in vibeNouns)
        foreach (var extra in vibeExtras)
            vibePrompts.Add($"{adj} {noun} {extra}");

        var widgetSizes = new[] { "huge", "tiny", "massive", "small", "compact", "" };
        var widgetTopics = new[] { "cpu monitor", "ram widget", "clock and date", "dashboard with everything", "battery status", "network graph", "storage space", "internet download speed", "processor bar" };
        var widgetPositions = new[] { "in the top left", "bottom right", "near the center", "top right", "bottom left", "" };

        var widgetPrompts = new List<string>();
        foreach (var size in widgetSizes)
        foreach (var topic in widgetTopics)
        foreach (var pos in widgetPositions)
            widgetPrompts.Add($"{size} {topic} {pos}".Trim());

        Console.WriteLine($"Generated {vibePrompts.Count} Vibe Prompts and {widgetPrompts.Count} Widget Prompts.\n");

        RunVibeTests(vibePrompts);
        RunWidgetTests(widgetPrompts);
    }

    static void RunVibeTests(List<string> prompts)
    {
        Console.WriteLine("--- Running Vibe Generation Tests ---");
        var gen = new VibeThemeGenerator();
        int successCount = 0;
        int errorCount = 0;
        int totalSwatches = 0;
        var sw = Stopwatch.StartNew();

        foreach (var prompt in prompts)
        {
            try
            {
                var (theme, analysis) = gen.GenerateAndExplain(prompt);
                if (theme != null && analysis != null)
                {
                    successCount++;
                    totalSwatches += analysis.Swatches.Count;
                }
            }
            catch
            {
                errorCount++;
            }
        }
        sw.Stop();

        Console.WriteLine($"Total Prompts Tested: {prompts.Count}");
        Console.WriteLine($"Successful: {successCount}");
        Console.WriteLine($"Failed/Crashed: {errorCount}");
        Console.WriteLine($"Average Time per Prompt: {sw.ElapsedMilliseconds / (double)prompts.Count:F2} ms");
        Console.WriteLine($"Total Colors Generated: {totalSwatches}\n");
    }

    static void RunWidgetTests(List<string> prompts)
    {
        Console.WriteLine("--- Running Widget Generation Tests ---");
        var gen = new WidgetVibeGenerator();
        int successCount = 0;
        int errorCount = 0;
        int totalMeasures = 0;
        int totalMeters = 0;
        int nonDefaultPosition = 0;
        int nonDefaultScale = 0;
        var sw = Stopwatch.StartNew();

        foreach (var prompt in prompts)
        {
            try
            {
                var (skin, analysis) = gen.GenerateAndExplain(prompt);
                if (skin != null && analysis != null)
                {
                    successCount++;
                    totalMeasures += skin.Measures.Count;
                    totalMeters += skin.Meters.Count;
                    if (skin.X != 40 || skin.Y != 40) nonDefaultPosition++;
                    if (Math.Abs(analysis.SizeScale - 1.0) > 0.01) nonDefaultScale++;
                }
            }
            catch
            {
                errorCount++;
            }
        }
        sw.Stop();

        Console.WriteLine($"Total Prompts Tested: {prompts.Count}");
        Console.WriteLine($"Successful: {successCount}");
        Console.WriteLine($"Failed/Crashed: {errorCount}");
        Console.WriteLine($"Average Time per Prompt: {sw.ElapsedMilliseconds / (double)prompts.Count:F2} ms");
        Console.WriteLine($"Total Measures Generated: {totalMeasures}");
        Console.WriteLine($"Total Meters Generated: {totalMeters}");
        Console.WriteLine($"Prompts that altered Position: {nonDefaultPosition}");
        Console.WriteLine($"Prompts that altered Scale: {nonDefaultScale}\n");
    }
}

