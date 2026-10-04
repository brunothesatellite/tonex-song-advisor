using TonexAdvisor.App.Config;
using TonexAdvisor.App.Services;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Two roles, never mixed: the voices propose, the arbitre decides. A voice that fails becomes an
/// opinion of its own, and a slow one never holds up the others.
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

    private static Voice Voice(string label, Func<CancellationToken, Task<string>> reply)
        => new(new FakeClient(label, reply), "test-model", label);

    [Fact]
    public async Task Consultation_KeepsEveryVoice_EvenWhenOneOfThemFails()
    {
        var voices = new[]
        {
            Voice("OpenCode · a", _ => Task.FromResult("Je choisis le preset 1.")),
            Voice("Gemini (Google)", _ => throw new InvalidOperationException("401 Unauthorized")),
            Voice("Groq", _ => Task.FromResult("Non, le preset 2 est meilleur.")),
        };

        var seen = new List<string>();

        var opinions = await AiConsultation.ConsultAsync(
            voices,
            "prompt",
            100,
            onOpinion: opinion => seen.Add(opinion.Provider));

        Assert.Equal(3, opinions.Count);
        Assert.Equal(2, opinions.Count(opinion => opinion.Ok));
        Assert.True(seen.Count == 3, "each voice must be reported as soon as it lands");

        var failure = opinions.Single(opinion => opinion.Provider == "Gemini (Google)");
        Assert.False(failure.Ok);
        Assert.Contains("401", failure.Error ?? "");
    }

    [Fact]
    public async Task Consultation_TimesOutASlowVoice_InsteadOfWaitingForIt()
    {
        var slow = Voice("Mistral", async cancellation =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellation);
            return "trop tard";
        });

        var opinions = await AiConsultation.ConsultAsync(
            new[] { slow },
            "prompt",
            100,
            timeout: TimeSpan.FromMilliseconds(200));

        var opinion = Assert.Single(opinions);
        Assert.False(opinion.Ok);
        Assert.Contains("Délai dépassé", opinion.Error ?? "");
        Assert.True(opinion.ElapsedMs < 10_000, $"it waited {opinion.ElapsedMs} ms");
    }

    [Fact]
    public void BuildVoices_TakesTheTickedOpenCodeModelsThenTheFreeVoices()
    {
        var config = new AppConfig
        {
            ApiKey = "oc_sk_test",
            Model = "longcat-2.5-preview-free",
            VoiceModels = { "space-bunny-free", "kimi-k2.6" },
            Providers =
            {
                ["gemini"] = new ProviderCredential { ApiKey = "AIza-test" },
                ["groq"] = new ProviderCredential { ApiKey = "gsk-test" },
            },
        };

        var labels = AiConsultation.BuildVoices(config)
            .Select(voice => voice.Label)
            .ToList();

        // Les modèles OpenCode dans l'ordre de la liste, puis les voix gratuites.
        Assert.Equal(
            new[]
            {
                "OpenCode · space-bunny-free",
                "OpenCode · kimi-k2.6",
                "Gemini (Google)",
                "Groq",
            },
            labels.ToArray());
    }

    [Fact]
    public void AVoiceWithoutAnExplicitModelUsesTheFreeDefaultOfItsProvider()
    {
        var config = new AppConfig
        {
            Providers = { ["groq"] = new ProviderCredential { ApiKey = "gsk-test" } },
        };

        var voice = Assert.Single(AiConsultation.BuildVoices(config));
        Assert.Equal("openai/gpt-oss-120b", voice.Model);
    }

    [Fact]
    public void TheArbitreIsNeverOneOfTheVoices()
    {
        var config = new AppConfig
        {
            ApiKey = "oc_sk_test",
            Model = "longcat-2.5-preview-free",
            VoiceModels = { "space-bunny-free" },
        };

        var arbitre = AiConsultation.BuildArbitre(config);

        Assert.NotNull(arbitre);
        Assert.Equal("longcat-2.5-preview-free", arbitre!.Model);
        Assert.DoesNotContain(
            AiConsultation.BuildVoices(config),
            voice => string.Equals(voice.Model, arbitre.Model, StringComparison.OrdinalIgnoreCase)
                     && voice.Label == arbitre.Label);
    }

    [Fact]
    public void WithoutAnOpenCodeKeyThereIsNoArbitre()
    {
        var config = new AppConfig
        {
            Providers = { ["groq"] = new ProviderCredential { ApiKey = "gsk-test" } },
        };

        Assert.Null(AiConsultation.BuildArbitre(config));
    }
}
