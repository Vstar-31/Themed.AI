using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Skins; // Assuming MeasureType is here

class Program
{
    record VibeTestCase(string Prompt, string[] ExpectedKeywords, bool ExpectDark);
    record WidgetTestCase(string Prompt, MeasureType[] ExpectedMeasures, double? ExpectedScaleDir, string ExpectedPosDir);

    static void Main()
    {
        Console.WriteLine("=== Generating 1000s of Prompts with Accuracy Testing ===");
        var reportPath = Path.Combine(Environment.CurrentDirectory, "StressTestReport.md");
        var sb = new StringBuilder();
        sb.AppendLine("# NLP Engine Stress Test Report\n");

        var vibeTests = GenerateVibeTests();
        var widgetTests = GenerateWidgetTests();
        
        sb.AppendLine($"**Total Prompts Generated:** {vibeTests.Count + widgetTests.Count} ({vibeTests.Count} Vibe, {widgetTests.Count} Widget)\n");
        Console.WriteLine($"Generated {vibeTests.Count} Vibe Prompts and {widgetTests.Count} Widget Prompts.");

        RunVibeTests(vibeTests, sb);
        RunWidgetTests(widgetTests, sb);

        File.WriteAllText(reportPath, sb.ToString());
        Console.WriteLine($"\nTesting Complete! Detailed report saved to: {reportPath}");
    }

    static List<VibeTestCase> GenerateVibeTests()
    {
        var tests = new List<VibeTestCase>();
        var adjs = new[] { ("dark", true), ("bright", false), ("neon", false), ("cozy", false), ("gloomy", true), ("minimalist", false), ("synthwave", false), ("warm", false), ("midnight", true) };
        var nouns = new[] { "ocean", "forest", "city", "cafe", "sunset", "matrix", "space", "attic", "cyberpunk", "hacker", "storm" };
        var extras = new[] { "with red accents", "lots of blue", "mostly black and white", "feeling nostalgic", "super chaotic", "very plain", "highly futuristic" };
        
        // Conversational wrappers to simulate complex, messy user input
        var wrappers = new[] { 
            "{0}", 
            "I want a {0} please", 
            "can u give me a {0}", 
            "make it {0}!!!", 
            "give me {0} bro",
            "literally just a {0} would be sick",
            "im thinking something like {0} if u know what i mean"
        };

        foreach (var (adj, isDark) in adjs)
        {
            foreach (var noun in nouns)
            {
                foreach (var extra in extras)
                {
                    foreach (var wrapper in wrappers)
                    {
                        string basePrompt = $"{adj} {noun} {extra}";
                        string prompt = string.Format(wrapper, basePrompt);
                        tests.Add(new VibeTestCase(prompt, new[] { adj, noun }, isDark));
                    }
                }
            }
        }
        return tests;
    }

    static List<WidgetTestCase> GenerateWidgetTests()
    {
        var tests = new List<WidgetTestCase>();
        var sizes = new[] { 
            ("huge", (double?)1.0), ("massive", (double?)1.0), ("giant", (double?)1.0),
            ("tiny", (double?)-1.0), ("small", (double?)-1.0), ("compact", (double?)-1.0), 
            ("", (double?)null) 
        };
        var topics = new[] { 
            ("cpu monitor", new[] { MeasureType.Cpu }), 
            ("ram widget", new[] { MeasureType.Memory }), 
            ("clock and date", new[] { MeasureType.Time, MeasureType.Date }), 
            ("battery status", new[] { MeasureType.Battery }), 
            ("network graph", new[] { MeasureType.NetworkDown }), 
            ("storage space", new[] { MeasureType.DiskFree }), 
            ("system uptime", new[] { MeasureType.Uptime }) 
        };
        var positions = new[] { 
            ("in the top left", "TL"), ("bottom right", "BR"), ("top right", "TR"), ("bottom left", "BL"), ("dead center", "C"), ("near the top", "T"), ("", "NONE") 
        };

        var wrappers = new[] { 
            "{0}", 
            "put a {0} corner", 
            "i need a {0} rn", 
            "yo can u hook me up with a {0}?",
            "slap a {0} on my screen",
            "just {0} thx"
        };

        foreach (var (sizeStr, scaleDir) in sizes)
        {
            foreach (var (topicStr, measures) in topics)
            {
                foreach (var (posStr, posDir) in positions)
                {
                    foreach (var wrapper in wrappers)
                    {
                        string basePrompt = $"{sizeStr} {topicStr} {posStr}".Trim();
                        if (string.IsNullOrWhiteSpace(basePrompt)) continue;
                        string prompt = string.Format(wrapper, basePrompt);
                        tests.Add(new WidgetTestCase(prompt, measures, scaleDir, posDir));
                    }
                }
            }
        }
        return tests;
    }

    static void RunVibeTests(List<VibeTestCase> tests, StringBuilder sb)
    {
        sb.AppendLine("## Vibe Generation Tests\n");
        Console.WriteLine("\n--- Vibe Generation Tests ---");
        var gen = new VibeThemeGenerator();
        int success = 0;
        int failedMatches = 0;
        var sw = Stopwatch.StartNew();

        foreach (var test in tests)
        {
            try
            {
                var (theme, analysis) = gen.GenerateAndExplain(test.Prompt);
                bool matchedAll = true;
                foreach(var kw in test.ExpectedKeywords) {
                    if (!analysis.MatchedKeywords.Any(m => m.Contains(kw, StringComparison.OrdinalIgnoreCase))) {
                        matchedAll = false;
                    }
                }

                if (matchedAll) 
                {
                    success++;
                    string log = $"- ✅ SUCCESS: '{test.Prompt}' | Matched: {string.Join(",", analysis.MatchedKeywords)}";
                    sb.AppendLine(log.Replace("✅ SUCCESS", "✅ **SUCCESS**"));
                    Console.WriteLine(log);
                }
                else 
                {
                    failedMatches++;
                    string log = $"- ❌ FAILED MATCH: '{test.Prompt}' | Expected: {string.Join(",", test.ExpectedKeywords)} | Found: {string.Join(",", analysis.MatchedKeywords)}";
                    sb.AppendLine(log.Replace("❌ FAILED MATCH", "❌ **FAILED MATCH**"));
                    Console.WriteLine(log);
                }
            }
            catch (Exception ex)
            {
                string log = $"- 💥 CRASH: '{test.Prompt}' | Error: {ex.Message}";
                sb.AppendLine(log.Replace("💥 CRASH", "💥 **CRASH**"));
                Console.WriteLine(log);
            }
        }
        sw.Stop();

        sb.AppendLine("\n### Vibe Report Card");
        sb.AppendLine($"- **Total Tested:** {tests.Count}");
        sb.AppendLine($"- **Accurate Extractions:** {success} ({success * 100.0 / tests.Count:F1}%)");
        sb.AppendLine($"- **Failed Matches:** {failedMatches}");
        sb.AppendLine($"- **Average Time:** {sw.ElapsedMilliseconds / (double)tests.Count:F2} ms");
        sb.AppendLine("---\n");
    }

    static void RunWidgetTests(List<WidgetTestCase> tests, StringBuilder sb)
    {
        sb.AppendLine("## Widget Generation Tests\n");
        Console.WriteLine("\n--- Widget Generation Tests ---");
        var gen = new WidgetVibeGenerator();
        int success = 0;
        int failedMatches = 0;
        var sw = Stopwatch.StartNew();

        foreach (var test in tests)
        {
            try
            {
                var (skin, analysis) = gen.GenerateAndExplain(test.Prompt);
                var skinMeasures = skin.Measures.Select(m => m.Type).ToList();
                
                bool matchedAllMeasures = true;
                foreach(var m in test.ExpectedMeasures) {
                    if (!skinMeasures.Contains(m)) matchedAllMeasures = false;
                }

                bool matchedScale = true;
                if (test.ExpectedScaleDir == 1.0 && analysis.SizeScale <= 1.0) matchedScale = false;
                if (test.ExpectedScaleDir == -1.0 && analysis.SizeScale >= 1.0) matchedScale = false;

                bool matchedPos = true;
                if (test.ExpectedPosDir == "BR" && (skin.X < 500 || skin.Y < 500)) matchedPos = false;
                if (test.ExpectedPosDir == "TL" && (skin.X > 500 || skin.Y > 500)) matchedPos = false;
                if (test.ExpectedPosDir == "TR" && (skin.X < 500 || skin.Y > 500)) matchedPos = false;
                if (test.ExpectedPosDir == "BL" && (skin.X > 500 || skin.Y < 500)) matchedPos = false;

                if (matchedAllMeasures && matchedScale && matchedPos) 
                {
                    success++;
                    string log = $"- ✅ SUCCESS: '{test.Prompt}' | Measures: {string.Join(",", skinMeasures)} | Scale: {analysis.SizeScale:F2} | Pos: ({skin.X}, {skin.Y})";
                    sb.AppendLine(log.Replace("✅ SUCCESS", "✅ **SUCCESS**"));
                    Console.WriteLine(log);
                } 
                else 
                {
                    failedMatches++;
                    var fails = new List<string>();
                    if (!matchedAllMeasures) fails.Add("Measures missed");
                    if (!matchedScale) fails.Add("Scale missed");
                    if (!matchedPos) fails.Add("Position missed");
                    string log = $"- ❌ FAILED MATCH: '{test.Prompt}' | Reason: {string.Join(", ", fails)}";
                    sb.AppendLine(log.Replace("❌ FAILED MATCH", "❌ **FAILED MATCH**"));
                    Console.WriteLine(log);
                }
            }
            catch (Exception ex)
            {
                string log = $"- 💥 CRASH: '{test.Prompt}' | Error: {ex.Message}";
                sb.AppendLine(log.Replace("💥 CRASH", "💥 **CRASH**"));
                Console.WriteLine(log);
            }
        }
        sw.Stop();

        sb.AppendLine("\n### Widget Report Card");
        sb.AppendLine($"- **Total Tested:** {tests.Count}");
        sb.AppendLine($"- **Accurate Extractions:** {success} ({success * 100.0 / tests.Count:F1}%)");
        sb.AppendLine($"- **Failed Matches:** {failedMatches}");
        sb.AppendLine($"- **Average Time:** {sw.ElapsedMilliseconds / (double)tests.Count:F2} ms");
        sb.AppendLine("---\n");
    }
}

