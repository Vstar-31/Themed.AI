using System;
using ThemeManager.Core.NLP;

class Program
{
    static void Main()
    {
        try
        {
            var gen = new VibeThemeGenerator();
            var res = gen.Explain("midnight ocean storm");
            Console.WriteLine($"Success: {res.Swatches.Length} swatches");
            foreach (var s in res.Swatches) Console.WriteLine(s);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }
}
