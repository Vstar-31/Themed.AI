namespace ThemeManager.Core.NLP;

/// <summary>
/// Martin Porter's stemming algorithm (1980), faithfully implemented in C#.
///
/// Python analogy: nltk.stem.PorterStemmer — exactly the same algorithm.
///
/// Converts inflected/derived words to their morphological root so the lexicon
/// can match "forests" → "forest", "oceanic" → "ocean", "burning" → "burn".
///
/// This is deliberately NOT a lemmatizer (which requires a dictionary).
/// Stemming is faster, needs zero data files, and is sufficient for color lexicon lookup.
/// </summary>
public static class PorterStemmer
{
    public static string Stem(string word)
    {
        if (word.Length <= 2) return word;
        word = word.ToLowerInvariant();
        word = Step1a(word);
        word = Step1b(word);
        word = Step1c(word);
        word = Step2(word);
        word = Step3(word);
        word = Step4(word);
        word = Step5a(word);
        word = Step5b(word);
        return word;
    }

    // ── Measurement m: count VC sequences in the stem ─────────────────────────
    private static bool IsConsonant(string w, int i)
    {
        char c = w[i];
        if (c is 'a' or 'e' or 'i' or 'o' or 'u') return false;
        if (c == 'y') return i == 0 || !IsConsonant(w, i - 1);
        return true;
    }

    private static int Measure(string s)
    {
        int n = 0, i = 0, len = s.Length;
        while (i < len && IsConsonant(s, i)) i++;  // skip consonant cluster
        while (i < len)
        {
            while (i < len && !IsConsonant(s, i)) i++;  // skip vowel cluster
            while (i < len && IsConsonant(s, i)) { i++; n++; }  // count VC
        }
        return n;
    }

    private static bool ContainsVowel(string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (!IsConsonant(s, i)) return true;
        return false;
    }

    private static bool EndsDoubleConsonant(string s)
    {
        int l = s.Length;
        return l >= 2 && s[l - 1] == s[l - 2] && IsConsonant(s, l - 1);
    }

    private static bool EndsCvc(string s)
    {
        int l = s.Length;
        if (l < 3) return false;
        return IsConsonant(s, l - 3) && !IsConsonant(s, l - 2) && IsConsonant(s, l - 1)
               && s[l - 1] is not 'w' and not 'x' and not 'y';
    }

    // ── Step 1a ───────────────────────────────────────────────────────────────
    private static string Step1a(string w)
    {
        if (w.EndsWith("sses")) return w[..^4] + "ss";
        if (w.EndsWith("ies"))  return w[..^3] + "i";
        if (w.EndsWith("ss"))   return w;
        if (w.EndsWith("s"))    return w[..^1];
        return w;
    }

    // ── Step 1b ───────────────────────────────────────────────────────────────
    private static string Step1b(string w)
    {
        if (w.EndsWith("eed"))
        {
            var stem = w[..^3];
            return Measure(stem) > 0 ? stem + "ee" : w;
        }
        bool flag = false;
        if (w.EndsWith("ed") && ContainsVowel(w[..^2]))  { w = w[..^2]; flag = true; }
        else if (w.EndsWith("ing") && ContainsVowel(w[..^3])) { w = w[..^3]; flag = true; }
        if (!flag) return w;

        if (w.EndsWith("at") || w.EndsWith("bl") || w.EndsWith("iz")) return w + "e";
        if (EndsDoubleConsonant(w) && w[^1] is not 'l' and not 's' and not 'z') return w[..^1];
        if (Measure(w) == 1 && EndsCvc(w)) return w + "e";
        return w;
    }

    // ── Step 1c ───────────────────────────────────────────────────────────────
    private static string Step1c(string w)
        => w.EndsWith('y') && ContainsVowel(w[..^1]) ? w[..^1] + "i" : w;

    // ── Step 2 ────────────────────────────────────────────────────────────────
    private static string Step2(string w)
    {
        var map = new (string suffix, string replace)[]
        {
            ("ational","ate"),("tional","tion"),("enci","ence"),("anci","ance"),
            ("izer","ize"),("bli","ble"),("alli","al"),("entli","ent"),
            ("eli","e"),("ousli","ous"),("ization","ize"),("ation","ate"),
            ("ator","ate"),("alism","al"),("iveness","ive"),("fulness","ful"),
            ("ousness","ous"),("aliti","al"),("iviti","ive"),("biliti","ble"),
            ("logi","log"),
        };
        foreach (var (s, r) in map)
            if (w.EndsWith(s) && Measure(w[..^s.Length]) > 0)
                return w[..^s.Length] + r;
        return w;
    }

    // ── Step 3 ────────────────────────────────────────────────────────────────
    private static string Step3(string w)
    {
        var map = new (string suffix, string replace)[]
        {
            ("icate","ic"),("ative",""),("alize","al"),
            ("iciti","ic"),("ical","ic"),("ful",""),("ness",""),
        };
        foreach (var (s, r) in map)
            if (w.EndsWith(s) && Measure(w[..^s.Length]) > 0)
                return w[..^s.Length] + r;
        return w;
    }

    // ── Step 4 ────────────────────────────────────────────────────────────────
    private static string Step4(string w)
    {
        var suffixes = new[]
        {
            "al","ance","ence","er","ic","able","ible","ant","ement",
            "ment","ent","ism","ate","iti","ous","ive","ize",
        };
        foreach (var s in suffixes)
            if (w.EndsWith(s) && Measure(w[..^s.Length]) > 1)
                return w[..^s.Length];
        if (w.EndsWith("ion") && Measure(w[..^3]) > 1 && w[^4] is 's' or 't')
            return w[..^3];
        return w;
    }

    // ── Step 5a ───────────────────────────────────────────────────────────────
    private static string Step5a(string w)
    {
        if (!w.EndsWith('e')) return w;
        var stem = w[..^1];
        int m = Measure(stem);
        if (m > 1 || (m == 1 && !EndsCvc(stem))) return stem;
        return w;
    }

    // ── Step 5b ───────────────────────────────────────────────────────────────
    private static string Step5b(string w)
        => Measure(w) > 1 && EndsDoubleConsonant(w) && w.EndsWith('l')
            ? w[..^1] : w;
}
