using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

public class TokenizerTests
{
    [Fact]
    public void Normalize_FoldsAccentsCasingAndSeparators()
    {
        Assert.Equal("creme brulee", Tokenizer.Normalize("Crème Brûlée"));
        Assert.Equal("guns n roses", Tokenizer.Normalize("Guns N’ Roses"));
        Assert.Equal("hi gain", Tokenizer.Normalize("  HI-GAIN  "));
    }

    [Fact]
    public void Normalize_TreatsEmptyAndWhitespaceAsEmpty()
    {
        Assert.Equal("", Tokenizer.Normalize(null));
        Assert.Equal("", Tokenizer.Normalize(""));
        Assert.Equal("", Tokenizer.Normalize("   \t "));
    }

    [Fact]
    public void Tokenize_DeduplicatesTokensAndSkipsBlankQueries()
    {
        Assert.Empty(Tokenizer.Tokenize(null));
        Assert.Empty(Tokenizer.Tokenize("   "));
        Assert.Equal(new List<string> { "hi", "gain" }, Tokenizer.Tokenize("HI-GAIN hi gain"));
    }

    [Fact]
    public void Contains_MatchesAcrossAccentDifferences()
    {
        Assert.True(Tokenizer.Contains("Crème Brûlée", "creme"));
        Assert.False(Tokenizer.Contains("Crème", "vanille"));
        Assert.True(Tokenizer.Contains("anything", "  "));
    }
}
