using System;
using ThemeManager.Core.NLP;
class Program { static void Main() {
    Console.WriteLine("vibe: " + PorterStemmer.Stem("vibe"));
    Console.WriteLine("mood: " + PorterStemmer.Stem("mood"));
    Console.WriteLine("recommend: " + PorterStemmer.Stem("recommend"));
} }
