using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Tests;

public class ChainBlockTests
{
    [Fact]
    public void ParseList_NullOrEmpty_YieldsEmptyChain()
    {
        Assert.Empty(ChainBlock.ParseList(null));
        Assert.Empty(ChainBlock.ParseList(""));
        Assert.Empty(ChainBlock.ParseList("   "));
    }

    [Fact]
    public void ParseList_MalformedJson_YieldsEmptyChainInsteadOfThrowing()
    {
        Assert.Empty(ChainBlock.ParseList("{ not json"));
        Assert.Empty(ChainBlock.ParseList("[1,2,3]"));
        Assert.Empty(ChainBlock.ParseList("\"a string\""));
    }

    [Fact]
    public void ParseList_RealChain_PreservesOrderAndBypassFlags()
    {
        const string json = "[\r\n  {\r\n    \"ID\": 0,\r\n    \"Bypass\": false\r\n  },\r\n" +
                            "  {\r\n    \"ID\": 12,\r\n    \"Bypass\": false\r\n  },\r\n" +
                            "  {\r\n    \"ID\": 1,\r\n    \"Bypass\": true\r\n  }\r\n]";

        var chain = ChainBlock.ParseList(json);

        Assert.Equal(3, chain.Count);
        Assert.Equal(0, chain[0].Id);
        Assert.False(chain[0].Bypass);
        Assert.Equal(12, chain[1].Id);
        Assert.Equal(1, chain[2].Id);
        Assert.True(chain[2].Bypass);
    }

    [Fact]
    public void ParseList_NegativeIds_AreTreatedAsEmptyPaddingSlots()
    {
        var chain = ChainBlock.ParseList("[{\"ID\": -1, \"Bypass\": true}, {\"ID\": 4, \"Bypass\": false}]");

        Assert.True(chain[0].IsEmpty);
        Assert.False(chain[1].IsEmpty);
        Assert.Single(chain, block => !block.IsEmpty);
    }
}
