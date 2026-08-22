using System;
using System.IO;
using ThemeManager.Core.NLP;
class Program { static void Main() {
    File.WriteAllText("G:\my projects\Themed.AI\Themed.AI\stems.txt", 
        PorterStemmer.Stem("vibe") + "\n" + 
        PorterStemmer.Stem("vibes") + "\n" + 
        PorterStemmer.Stem("vibefinder") + "\n" + 
        PorterStemmer.Stem("mood") + "\n" +
        PorterStemmer.Stem("recommend")
    );
} }
