namespace ThemeManager.Tests;
using Xunit;
using Xunit.Abstractions;
using ThemeManager.Core.NLP;
public class StemmingTests {
    private readonly ITestOutputHelper _output;
    public StemmingTests(ITestOutputHelper output) { _output = output; }
    [Fact]
    public void PrintStems() {
        _output.WriteLine("STEM vibe: " + PorterStemmer.Stem("vibe"));
        _output.WriteLine("STEM mood: " + PorterStemmer.Stem("mood"));
        _output.WriteLine("STEM recommend: " + PorterStemmer.Stem("recommend"));
    }
}
