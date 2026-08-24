using System;
using ThemeManager.Core.NLP;
class Program {
    static void Main() {
        Console.WriteLine("circular -> " + PorterStemmer.Stem("circular"));
        Console.WriteLine("ring -> " + PorterStemmer.Stem("ring"));
        Console.WriteLine("gauge -> " + PorterStemmer.Stem("gauge"));
    }
}
