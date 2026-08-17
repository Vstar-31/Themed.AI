using System;
using ThemeManager.Core.NLP;

class Program
{
    static void Main()
    {
        try
        {
            var gen = new VibeThemeGenerator();
            var (theme, analysis) = gen.GenerateAndExplain("midnight ocean storm");
            Console.WriteLine($"Success: {analysis.Swatches.Count} swatches");
            foreach (var s in analysis.Swatches) Console.WriteLine(s);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }
}
