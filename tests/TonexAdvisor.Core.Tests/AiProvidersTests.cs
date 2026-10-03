using TonexAdvisor.App.Config;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The crossed panel is configured by keys the user chooses to fill in: the catalogue must name
/// a real provider, a free default model and a page where a key can be asked for.
/// </summary>
public class AiProvidersTests
{
    [Fact]
    public void Challengers_AreGeminiMistralAndGroq_WithTheirKeyPage()
    {
        Assert.Equal(
            new[] { "gemini", "mistral", "groq" },
            AiProviders.Challengers.Select(provider => provider.Id).ToArray());

        foreach (var provider in AiProviders.Challengers)
        {
            Assert.False(string.IsNullOrWhiteSpace(provider.Label));
            Assert.False(string.IsNullOrWhiteSpace(provider.DefaultModel));
            Assert.StartsWith("https://", provider.EndPoint, StringComparison.Ordinal);
            Assert.StartsWith("https://", provider.KeyUrl, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Find_IgnoresCase_AndRejectsUnknownIds()
    {
        Assert.Equal("Gemini (Google)", AiProviders.Find("GEMINI")!.Label);
        Assert.Equal("Groq", AiProviders.Find("groq")!.Label);
        Assert.Null(AiProviders.Find("copilot"));
        Assert.Null(AiProviders.Find(null));
    }

    [Fact]
    public void AppConfig_KeepsOnlyTheKeysTheUserFilledIn()
    {
        var file = Path.Combine(Path.GetTempPath(), "tonex-config-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var config = new AppConfig
            {
                ApiKey = "oc_sk_test",
                Providers =
                {
                    ["gemini"] = new ProviderCredential { ApiKey = "AIza-test", Model = "gemini-2.5-flash" },
                },
            };

            config.Save(file);
            var loaded = AppConfig.Load(file);

            Assert.Equal("AIza-test", loaded.CredentialFor("gemini").ApiKey);
            Assert.Equal("gemini-2.5-flash", loaded.CredentialFor("gemini").Model);

            // Not filled in: the voice stays silent, and nothing blows up.
            Assert.Equal("", loaded.CredentialFor("groq").ApiKey);
            Assert.Equal("", loaded.CredentialFor("mistral").Model);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
