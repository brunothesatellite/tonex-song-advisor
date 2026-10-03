using TonexAdvisor.App.Services;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The stream is the only part of the AI that cannot be tested against a live service, so it is
/// tested line by line here: what arrives must be shown, what is noise must not.
/// </summary>
public class SseParserTests
{
    [Fact]
    public void Feed_AccumulatesContent_AndStopsAtDone()
    {
        var parser = new SseParser();

        var first = parser.Feed("""data: {"choices":[{"delta":{"content":"Bon"}}]}""");
        var second = parser.Feed("""data: {"choices":[{"delta":{"content":"jour"}}]}""");
        var done = parser.Feed("data: [DONE]");

        Assert.NotNull(first);
        Assert.False(first!.IsReasoning);
        Assert.NotNull(second);
        Assert.Null(done);

        Assert.Equal("Bonjour", parser.Content);
        Assert.Equal("", parser.Reasoning);
        Assert.True(parser.IsDone);
    }

    [Fact]
    public void Feed_SeparatesTheChainOfThoughtFromTheAnswer()
    {
        var parser = new SseParser();

        var thinking = parser.Feed("""data: {"choices":[{"delta":{"reasoning_content":"je cherche le bon gain "}}]}""");
        var answer = parser.Feed("""data: {"choices":[{"delta":{"content":"Commence par le preset 1."}}]}""");

        Assert.NotNull(thinking);
        Assert.True(thinking!.IsReasoning);
        Assert.NotNull(answer);
        Assert.False(answer!.IsReasoning);

        Assert.Equal("je cherche le bon gain ", parser.Reasoning);
        Assert.Equal("Commence par le preset 1.", parser.Content);
    }

    [Fact]
    public void Feed_IgnoresKeepAlivesCommentsAndBrokenLines()
    {
        var parser = new SseParser();

        Assert.Null(parser.Feed(""));
        Assert.Null(parser.Feed(": keep-alive"));
        Assert.Null(parser.Feed("event: message"));
        Assert.Null(parser.Feed("data: {\"choices\":[{\"delta\":{\"content\":\"moit"));
        Assert.Null(parser.Feed("""data: {"choices":[{"delta":{"content":""}}]}"""));

        Assert.Equal("", parser.Content);
        Assert.Equal("", parser.Reasoning);
        Assert.False(parser.IsDone);
    }

    [Fact]
    public void Feed_ToleratesAStreamWithoutChoices()
    {
        var parser = new SseParser();

        Assert.Null(parser.Feed("""data: {"id":"chatcmpl-1"}"""));
        Assert.Null(parser.Feed("""data: {"choices":[]}"""));
        Assert.Equal("", parser.Content);
    }

    [Fact]
    public void Feed_AcceptsThePlainTextField()
    {
        // Some providers name the answer `text` instead of `content`.
        var parser = new SseParser();

        var delta = parser.Feed("""data: {"choices":[{"delta":{"text":"Bonjour"}}]}""");

        Assert.NotNull(delta);
        Assert.False(delta!.IsReasoning);
        Assert.Equal("Bonjour", parser.Content);
    }

    [Fact]
    public void Feed_AcceptsAFinalMessageInsteadOfADelta()
    {
        // Some providers close the stream with `message` rather than `delta`.
        var parser = new SseParser();

        parser.Feed("""data: {"choices":[{"delta":{"content":"Bon"}}]}""");
        var last = parser.Feed("""data: {"choices":[{"message":{"content":"jour"}}]}""");

        Assert.NotNull(last);
        Assert.Equal("Bonjour", parser.Content);
    }

    [Fact]
    public void Feed_PrefersTheAnswerWhenAPayloadCarriesBoth()
    {
        var parser = new SseParser();

        var delta = parser.Feed(
            """data: {"choices":[{"delta":{"reasoning_content":"je pense ","content":"Bonjour"}}]}""");

        Assert.NotNull(delta);
        Assert.False(delta!.IsReasoning);
        Assert.Equal("Bonjour", parser.Content);
    }
}
