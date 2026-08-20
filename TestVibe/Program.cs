using System;
using System.Text.Json;
using ThemeManager.Core.NLP;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var prompt = args.Length > 0 ? args[0] : "I want a minimal widget that shows all of my disk spaces and memory in the bottom right";
            var skin = WidgetVibeGenerator.BuildSkin(prompt, 1920, 1080);
            var json = JsonSerializer.Serialize(skin, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }
}
