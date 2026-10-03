using TonexAdvisor.App.Config;
using TonexAdvisor.App.Services;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A panel is only useful if it survives one voice failing: every opinion must come back,
/// including the failed ones, and no voice may hold up the others.
/// </summary>
public class AiConsultationTests
{
    private sealed class FakeClient : IAiClient
    {
        private readonly Func<CancellationToken, Task<string>> _reply;

        public FakeClient(string provider, Func<CancellationToken, Task<string>> reply)
        {
            Provider = provider;
            _reply = reply;
        }

        public string Provider { get; }

        public Task<string> AskStreamAsync(
            string model,
            string prompt,
            int maxTokens,
            Action<SseDelta> onDelta,
            CancellationToken cancellationToken = default)
            => _reply(cancellationToken);
    }

    [Fact]
    public async Task Consultation_KeepsEveryVoice_EvenWhenOneOfThemFails()
    {
        var clients = new IAiClient[]
        {
            new FakeClient("Gemini (Google)", _ => Task.FromResult("Je choisis le preset 1.")),
            new FakeClient("Mistral", _ => throw new InvalidOperationException("401 Unauthorized")),
            new FakeClient("Groq", _ => Task.FromResult("Non, le preset 2 est meilleur.")),
        };

        var seen = new List<string>();

        var opinions = await AiConsultation.ConsultAsync(
            clients,
            _ => "test-model",
            "prompt",
            100,
            TimeSpan.FromSeconds(30),
            onOpinion: opinion => seen.Add(opinion.Provider));

        Assert.Equal(3, opinions.Count);
        Assert.Equal(2, opinions.Count(opinion => opinion.Ok));
        Assert.True(seen.Count == 3, "each voice must be reported as soon as it lands");

        var failure = opinions.Single(opinion => opinion.Provider == "Mistral");
        Assert.False(failure.Ok);
        Assert.Contains("401", failure.Error ?? "");
    }

    [Fact]
    public async Task Consultation_TimesOutASlowVoice_InsteadOfWaitingForIt()
    {
        var slow = new FakeClient("Mistral", async cancellation =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellation);
            return "trop tard";
        });

        var opinions = await AiConsultation.ConsultAsync(
            new IAiClient[] { slow },
            _ => "test-model",
            "prompt",
            100,
            TimeSpan.FromMilliseconds(200));

        var opinion = Assert.Single(opinions);
        Assert.False(opinion.Ok);
        Assert.Contains("Délai dépassé", opinion.Error ?? "");
        Assert.True(opinion.ElapsedMs < 10_000, $"it waited {opinion.ElapsedMs} ms");
    }

    [Fact]
    public void BuildClients_TakesOnlyTheVoicesWithAKey()
    {
        var silent = new AppConfig { ApiKey = "" };
        Assert.Empty(AiConsultation.BuildClients(silent));

        var oneVoice = new AppConfig { ApiKey = "oc_sk_test" };
        Assert.Single(AiConsultation.BuildClients(oneVoice));

        var panel = new AppConfig
        {
            ApiKey = "oc_sk_test",
            Providers =
            {
                ["gemini"] = new ProviderCredential { ApiKey = "AIza-test" },
                ["groq"] = new ProviderCredential { ApiKey = "gsk-test" },
            },
        };

        var labels = AiConsultation.BuildClients(panel).Select(client => client.Provider).ToList();

        Assert.Equal(new[] { "OpenCode Go", "Gemini (Google)", "Groq" }, labels.ToArray());
    }

    [Fact]
    public void ModelFor_FallsBackToTheFreeModelOfTheCatalogue()
    {
        var config = new AppConfig
        {
            Model = "longcat-2.5-preview-free",
            Providers =
            {
                ["groq"] = new ProviderCredential { ApiKey = "gsk-test", Model = "llama-custom" },
            },
        };

        // The reference voice uses the model chosen in the settings.
        Assert.Equal("longcat-2.5-preview-free", AiConsultation.ModelFor(config, "OpenCode Go"));

        // A challenger without an explicit model gets the free default of its catalogue.
        Assert.Equal("gemini-flash-latest", AiConsultation.ModelFor(config, "Gemini (Google)"));

        // And one with an explicit model keeps it.
        Assert.Equal("llama-custom", AiConsultation.ModelFor(config, "Groq"));
    }
}
