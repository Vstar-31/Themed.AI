using System;
using ThemeManager.Core.NLP;
class Program {
    static void Main() {
        string[] words = { "icon", "icons", "glyph", "image", "symbol", "ring", "rings", "circle", "circular", "gauge" };
        foreach (var w in words) {
            Console.WriteLine(w + " -> " + PorterStemmer.Stem(w));
        }
    }
}
