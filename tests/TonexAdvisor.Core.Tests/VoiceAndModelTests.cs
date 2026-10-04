using TonexAdvisor.App.Config;
using TonexAdvisor.App.Services;
using TonexAdvisor.App.ViewModels;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A key is not enough to be consulted: a voice the user switched off is neither called nor
/// shown. And the model list comes from the API — free and paid — with a fallback when the chosen
/// model has disappeared.
/// </summary>
public class VoiceAndModelTests
{
    private sealed class FakeHost : IDatabaseHost
    {
        public ToneXDatabase? CurrentDatabase => null;

        public Task LoadDatabaseAsync(string path) => Task.CompletedTask;
    }

    private static string EmptyFolder()
        => Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "tonex-vm-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public void A_disabled_voice_is_not_consulted_even_with_its_key()
    {
        var config = new AppConfig
        {
            ApiKey = "oc_sk_test",
            Providers =
            {
                ["gemini"] = new ProviderCredential { ApiKey = "AIza-test" },
                ["groq"] = new ProviderCredential { ApiKey = "gsk-test" },
            },
        };

        var state = new UserState { DisabledVoices = new List<string> { "gemini" } };
        var labels = AiConsultation.BuildClients(config, state)
            .Select(client => client.Provider)
            .ToList();

        Assert.Equal(new[] { "OpenCode Go", "Groq" }, labels.ToArray());
    }

    [Fact]
    public void Everything_switched_off_leaves_the_panel_empty()
    {
        var config = new AppConfig
        {
            ApiKey = "oc_sk_test",
            Providers = { ["groq"] = new ProviderCredential { ApiKey = "gsk-test" } },
        };

        var state = new UserState
        {
            DisabledVoices = new List<string> { "opencode", "gemini", "mistral", "groq" },
        };

        Assert.Empty(AiConsultation.BuildClients(config, state));
    }

    [Fact]
    public void Switching_a_voice_off_is_remembered()
    {
        var folder = EmptyFolder();

        try
        {
            var store = new InMemoryUserStateStore();
            var viewModel = new SettingsViewModel(new FakeHost(), store, folder);

            viewModel.OpenCodeActive = false;
            viewModel.GeminiActive = false;

            var state = store.Load();
            Assert.Contains(AiProviders.OpenCodeId, state.DisabledVoices);
            Assert.Contains("gemini", state.DisabledVoices);
            Assert.DoesNotContain("groq", state.DisabledVoices);
            Assert.DoesNotContain("mistral", state.DisabledVoices);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_model_that_disappeared_falls_back_on_a_free_one()
    {
        var folder = EmptyFolder();

        try
        {
            var viewModel = new SettingsViewModel(
                new FakeHost(),
                new InMemoryUserStateStore(),
                folder,
                _ => Task.FromResult<IReadOnlyList<string>>(
                    new[] { "glm-5.3-flash", "kimi-k3", "space-bunny-free" }));

            viewModel.ApiKey = "oc_sk_test";
            await viewModel.RefreshModelsCommand.ExecuteAsync(null);

            // LongCat absent : le premier gratuit, et on le dit.
            Assert.Equal("space-bunny-free", viewModel.SelectedModel!.Id);
            Assert.Contains("n'est plus disponible", viewModel.ModelStatus, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task LongCat_is_preferred_when_it_is_still_there()
    {
        var folder = EmptyFolder();

        try
        {
            var viewModel = new SettingsViewModel(
                new FakeHost(),
                new InMemoryUserStateStore(),
                folder,
                _ => Task.FromResult<IReadOnlyList<string>>(
                    new[] { "glm-5.3-flash", "longcat-2.5-preview-free" }));

            viewModel.ApiKey = "oc_sk_test";
            await viewModel.RefreshModelsCommand.ExecuteAsync(null);

            Assert.Equal("longcat-2.5-preview-free", viewModel.SelectedModel!.Id);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Paid_models_are_offered_and_say_so()
    {
        var folder = EmptyFolder();

        try
        {
            var viewModel = new SettingsViewModel(
                new FakeHost(),
                new InMemoryUserStateStore(),
                folder,
                _ => Task.FromResult<IReadOnlyList<string>>(new[] { "glm-5.3-flash", "space-bunny-free" }));

            viewModel.ApiKey = "oc_sk_test";
            await viewModel.RefreshModelsCommand.ExecuteAsync(null);

            Assert.Equal(2, viewModel.AvailableModels.Count);
            Assert.False(viewModel.AvailableModels[0].IsFree);
            Assert.Contains("payant", viewModel.AvailableModels[0].Label, StringComparison.Ordinal);
            Assert.Contains("gratuit", viewModel.AvailableModels[1].Label, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
